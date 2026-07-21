using KdTree;
using static Delauney.Util.Combinations;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using Delauney.Distances;
using Distance = Delauney.Distances.Distance;
using System.Collections;
using System.Collections.Concurrent;
using Delauney.Triangulation.Core;
using System.Threading.Tasks;

namespace Delauney.Triangulation;

/// <summary>
/// Helpers for suppressing the SynchronizationContext so that <c>await Task.Run(…)</c>
/// inside a WinForms event handler does not marshal continuations back to the UI thread
/// while the triangulation is in progress.
/// </summary>
public static class NoSynchronizationContextScope
{
    /// <summary>Clears the current SynchronizationContext and returns a disposable that restores it.</summary>
    public static Disposable Enter()
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        return new Disposable(context);
    }

    /// <summary>Restores the SynchronizationContext captured at <see cref="Enter"/> time.</summary>
    public struct Disposable : IDisposable
    {
        private readonly SynchronizationContext _synchronizationContext;
        public Disposable(SynchronizationContext synchronizationContext)
        {
            _synchronizationContext = synchronizationContext;
        }
        public void Dispose() =>
            SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
    }
}

/// <summary>
/// n-dimensional Delaunay triangulator using divide-and-conquer with ball-pivoting.
/// </summary>
/// <typeparam name="TVertex">Vertex type; must implement <see cref="IVertex"/> and have a public parameterless constructor.</typeparam>
/// <typeparam name="TFace">Simplex cell type; must extend <see cref="TriangulationCell{TVertex,TFace}"/> and have a public parameterless constructor.</typeparam>
/// <remarks>
/// <b>Algorithm outline</b>
/// <list type="number">
///   <item><b>Partition</b> — split the point set at the median of the highest-variance axis.</item>
///   <item><b>Seed simplex</b> — find d+1 points straddling the split plane to seed the triangulation.</item>
///   <item><b>Boundary stitching</b> (AFLalpha loop) — grow simplices on the split-plane boundary
///       by ball-pivoting: for each open face, find the apex whose circumball is smallest and empty.</item>
///   <item><b>Recurse in parallel</b> — the two half-partitions are independent after stitching
///       and are triangulated concurrently via <c>Parallel.Invoke</c>.</item>
/// </list>
///
/// <b>Circumball computation</b><br/>
/// The hot path uses a fast d×d linear system (precomputed per face, stack-allocated scratch arrays).
/// A Fiedler/Cayley-Menger fallback handles the under-complete faces during seed construction.
///
/// <b>Local candidate heuristic</b><br/>
/// When <see cref="UseLocalCandidates"/> is <c>true</c> (default) and the candidate pool is larger
/// than K = Dimensions×12, a KD-tree KNN query restricts the apex search to the K nearest points.
/// Correctness is guaranteed by an O(N−K) linear scan that falls back to full search if any
/// non-local point violates the Delaunay condition.
/// </remarks>
public class DelauneyTriangulator<TVertex, TFace>
        where TVertex : IVertex, new()
        where TFace : TriangulationCell<TVertex, TFace>, new()
{
    const int MAX_THREADS = 8;
    SemaphoreSlim sem = new(MAX_THREADS);

    private Normalizator<TVertex> Normalizator;
    private MiniBaller<TVertex> MiniBaller;

    /// <summary>Numerical tolerance used for coplanarity and half-space tests.</summary>
    public double Epsilon { get; init; }

    /// <summary>
    /// When <c>true</c>, fires <see cref="RaiseTriangulationEvent"/> at each algorithmic step.
    /// Also disables parallel recursion (events are not thread-safe with WinForms).
    /// </summary>
    public bool WithEvents { get; init; }

    /// <summary>
    /// When <c>true</c> (the default), restricts each apex search to the K nearest neighbours
    /// of the face centroid (K = Dimensions × 12). Provides near-linear scaling at large N.
    /// Disable for very small point sets where the KNN overhead exceeds the search savings.
    /// </summary>
    public bool UseLocalCandidates { get; init; } = true;

    /// <summary>
    /// Initialises the triangulator.
    /// </summary>
    /// <param name="withEvents">Set <c>true</c> to enable step-by-step visualisation events (disables parallel recursion).</param>
    /// <param name="epsilon">Numerical tolerance for coplanarity and half-space tests.</param>
    public DelauneyTriangulator(bool withEvents = false, double epsilon = 1e-10)
    {
        WithEvents = withEvents;
        Epsilon = epsilon;
    }

    /// <summary>
    /// Raised after each significant algorithmic step when <see cref="WithEvents"/> is <c>true</c>.
    /// The event payload describes the step type (face, ball, triangle, plane, …) and the
    /// relevant geometry.
    /// </summary>
    public event EventHandler<TriangulationEventArgs> RaiseTriangulationEvent;

    private int Dimensions { get; set; }
    private IList<TVertex> Positions { get; set; }

    protected virtual void OnRaiseTriangulationEvent(TriangulationEventArgs e)
    {
        EventHandler<TriangulationEventArgs> raiseEvent = RaiseTriangulationEvent;
        if (raiseEvent != null && WithEvents)
            raiseEvent(this, e);
    }

    /// <summary>
    /// Represents the median hyperplane used to split the point set.
    /// Also tracks which faces and points lie on each side.
    /// </summary>
    class AlphaPlane
    {
        public double Epsilon { get; init; } = double.Epsilon;
        public AlphaPlane(double epsilon = 1e-10) { Epsilon = epsilon; }

        /// <summary>Returns <c>true</c> when the simplex straddles the split plane (has vertices on both sides).</summary>
        public bool Intersects(IList<int> vertices, IList<TVertex> positions) =>
            IsLeft(vertices, positions) && IsRight(vertices, positions);

        /// <summary>Returns <c>true</c> when at least one vertex is at or left of the median.</summary>
        public bool IsLeft(IList<int> vertices, IList<TVertex> positions) =>
            vertices.Any(x => positions[x].Position[SplitDimension] - Median <= Epsilon);

        /// <summary>Returns <c>true</c> when at least one vertex is strictly right of the median.</summary>
        public bool IsRight(IList<int> vertices, IList<TVertex> positions) =>
            vertices.Any(x => positions[x].Position[SplitDimension] - Median > Epsilon);

        /// <summary>Corner matrix of the hyperplane (used for event rendering only).</summary>
        public Matrix<double> Plane { get; set; }
        public double Median { get; set; }
        public int SplitDimension { get; set; }
    }

    // KD-tree over all input points; used for KNN queries in the local candidate heuristic.
    KdTree<double, int> tree;

    // Tracks every simplex added so far. ConcurrentDictionary allows safe parallel access
    // from the two recursive sub-problems once the boundary stitching pass is complete.
    ConcurrentDictionary<SimplexComparable, byte> completed;

    /// <summary>
    /// Entry point. Builds and returns the Delaunay triangulation of <paramref name="P"/>.
    /// </summary>
    /// <param name="P">Input point set. All points must have the same dimension.</param>
    /// <returns>A triangulation whose <see cref="ITriangulation{TVertex,TFace}.Cells"/> enumerate the simplices.</returns>
    public ITriangulation<TVertex, TFace> CreateDelaunay(IList<TVertex> P)
    {
        Positions  = P;
        Dimensions = P.First().Position.Length;
        Normalizator = new(Dimensions);
        MiniBaller   = new(Dimensions);
        completed = new ConcurrentDictionary<SimplexComparable, byte>(new SimplexComparer<SimplexComparable>());

        // Build a single KD-tree over all points for use by the local candidate heuristic.
        tree = new KdTree<double, int>(Dimensions, new KdTree.Math.DoubleMath());
        foreach (var x in P.Select((x, i) => (x, i)))
            tree.Add(x.x.Position, x.i);

        var p = DelauneyInternal(P.Select((x, i) => i).ToList(), new FaceList());
        return new Triangulation<TVertex, TFace>()
        {
            Faces = p.Select(x => new TFace() { Vertices = x.Select(d => P[d]).ToArray() }).ToList()
        };
    }

    bool Equal(IList<int> l1, IList<int> l2) =>
        !(l1.Except(l2).Any() || l2.Except(l1).Any());

    bool Equal(IList<IList<int>> l1, IList<IList<int>> l2)
    {
        if (l1.Count != l2.Count) return false;
        return l1.All(x => l2.Any(y => Equal(x, y)));
    }

    /// <summary>
    /// Recursive divide-and-conquer triangulation of the point indices in <paramref name="P"/>.
    /// </summary>
    /// <param name="P">Point indices to triangulate.</param>
    /// <param name="AFL">Active Face List inherited from the parent call (faces that still need an apex).</param>
    /// <returns>List of simplices, each represented as a list of d+1 point indices.</returns>
    private List<List<int>> DelauneyInternal(IList<int> P, FaceList AFL)
    {
        FaceList AFLalpha = new(); // Faces on the split-plane boundary — processed in this call.
        FaceList AFL1 = new();    // Faces entirely in P1 — forwarded to the left recursion.
        FaceList AFL2 = new();    // Faces entirely in P2 — forwarded to the right recursion.
        IList<int> t = null;
        List<List<int>> Result = new();

        // Step 1: partition P by the highest-variance axis.
        (var P1, var P2, var alpha) = PointsetPartition(P, Positions);
        OnRaiseTriangulationEvent(new TriangulationEventArgs()
        {
            Transient = true,
            Type = TriangulationEventType.Plane,
            Plane = alpha.Plane.EnumerateRows().Select(x => x.ToArray()).ToArray()
        });

        AFL ??= new();

        if (AFL.Count == 0)
        {
            // Step 2: no inherited faces — build the seed simplex that straddles the split plane.
            t = MakeFirstSimplex(P, alpha, Positions);
            if (t == null) return Result;
            Result.Add(t.ToList());
            completed.TryAdd(new SimplexComparable(t), 0);

            // Seed the AFL with all faces of the first simplex, each with the opposite vertex noted.
            AFL = new FaceList(t.ToArray().GetDifferentCombinations(Dimensions).ToList()
                    .Join(t, x => 1, x => 1, (x, y) => (x, y))
                    .Where(x => !x.x.Contains(x.y)).ToArray());
        }

        // Distribute inherited faces to the correct active lists.
        foreach (var f in AFL)
        {
            if (alpha.Intersects(f.Face, Positions))
                AFLalpha.Add(f);
            if (f.Face.All(x => P1.Contains(x)))
                AFL1.Add(f);
            if (f.Face.All(x => P2.Contains(x)))
                AFL2.Add(f);
        }

        // Step 3: stitch the split-plane boundary. Process faces one at a time
        // (sequential — each new simplex may add faces that depend on prior ones).
        while (AFLalpha.Count != 0)
        {
            var f = AFLalpha.First();
            AFLalpha.RemoveFirst();
            t = MakeSimplex(f.Face, P, Positions, f.Opposite);
            if (t != null)
            {
                Result.Add(t.ToList());
                completed.TryAdd(new SimplexComparable(t), 0);
                OnRaiseTriangulationEvent(new TriangulationEventArgs()
                {
                    Triangle  = t.Select(x => Positions[x].Position).ToArray(),
                    Transient = false,
                    Type      = TriangulationEventType.Triangle
                });

                var scf = new SimplexComparable(f.Face);
                // Each new face of the new simplex (other than the face we just closed):
                //   • FaceList.Update toggles: if the face is already pending, remove it
                //     (both sides are now filled); if new, add it as an open face.
                foreach (var fprime in t.GetDifferentCombinations(Dimensions).ToList()
                    .Join(t, x => 1, x => 1, (x, y) => (Face: x, Opposite: y))
                    .Where(x => !x.Face.Contains(x.Opposite))
                    .Where(x => !(new SimplexComparable(x.Face)).Equals(scf)))
                {
                    if (alpha.Intersects(fprime.Face, Positions))
                        AFLalpha.Update(fprime);
                    if (fprime.Face.All(x => P1.Contains(x)))
                        AFL1.Update(fprime);
                    if (fprime.Face.All(x => P2.Contains(x)))
                        AFL2.Update(fprime);
                }
            }
        }

        // Step 4: the two half-partitions are now fully independent.
        // Run them in parallel when the point set is large enough to justify the overhead
        // and when events are disabled (event callbacks are not thread-safe with WinForms).
        if (!WithEvents && AFL1.Count != 0 && AFL2.Count != 0 && P.Count >= 32)
        {
            List<List<int>> r1 = null, r2 = null;
            Parallel.Invoke(
                () => r1 = DelauneyInternal(P1, AFL1),
                () => r2 = DelauneyInternal(P2, AFL2));
            Result.AddRange(r1);
            Result.AddRange(r2);
        }
        else
        {
            if (AFL1.Count != 0) Result.AddRange(DelauneyInternal(P1, AFL1));
            if (AFL2.Count != 0) Result.AddRange(DelauneyInternal(P2, AFL2));
        }
        return Result;
    }

    /// <summary>
    /// Finds the best apex for the given face, implementing the local candidate heuristic
    /// with verify-then-fallback for correctness.
    /// </summary>
    /// <param name="actual">Vertex indices of the open face (d indices for a full face).</param>
    /// <param name="positions">Full position list.</param>
    /// <param name="P">Candidate point indices to search.</param>
    /// <param name="opposite">
    /// Index of the vertex on the already-filled side of this face, used to enforce
    /// the half-space constraint (the new apex must be on the opposite side).
    /// </param>
    /// <returns>Index of the best apex, or <c>null</c> if no valid apex exists.</returns>
    int? NextPoint(IList<int> actual, IList<TVertex> positions, IList<int> P, int? opposite = null)
    {
        // Precompute the d−1 constant rows of the circumball linear system once per face.
        // fc is null when the face is not yet full (early MakeFirstSimplex steps in d≥3),
        // in which case DelauneyDistance falls back to the Cayley-Menger formula.
        FaceCircumsphere? fc = actual.Count == Dimensions
            ? MiniBaller.PrecomputeFace(actual, positions)
            : (FaceCircumsphere?)null;

        // Iterates over candidates and returns the one with the minimum signed Delaunay radius.
        // Negative radius means the circumball is on the correct (opposite) side of the face.
        (int? apex, double dd, double[] center) RunSearch(IList<int> candidates)
        {
            var nc = Normalizator.NormalAndCentroid(actual.ToArray(), positions);
            double maxradius = double.PositiveInfinity;
            int? item = null;
            double[] bestCenter = null;
            foreach (var n in candidates)
            {
                // Skip points on the same side as `opposite` — the new simplex must grow away from it.
                if (opposite != null &&
                    GeometryHelpers.PointSame(nc.Normal, nc.Centroid,
                        Positions[n].Position, positions[opposite.Value].Position, Epsilon))
                    continue;
                // Skip coplanar points — they would produce a degenerate simplex.
                if (GeometryHelpers.CoplanarAfin(positions[n].Position, nc.Normal, nc.Centroid, Epsilon))
                    continue;
                // Skip simplices we have already completed (prevents stitching the same face twice).
                if (completed.ContainsKey(new SimplexComparable(actual.Append(n))))
                    continue;
                OnRaiseTriangulationEvent(new TriangulationEventArgs()
                {
                    Transient = true,
                    Type      = TriangulationEventType.Triangle,
                    Triangle  = actual.Select(x => Positions[x].Position).Append(Positions[n].Position).ToArray()
                });
                var dd = DelauneyDistance(actual, opposite, n, Positions, fc);
                if (!dd.dd.IsFinite()) continue;
                OnRaiseTriangulationEvent(new TriangulationEventArgs()
                {
                    Center    = dd.center,
                    Radius    = Math.Abs(dd.dd),
                    Transient = true,
                    Type      = TriangulationEventType.Ball
                });
                if (dd.dd < maxradius)
                {
                    maxradius  = dd.dd;
                    item       = n;
                    bestCenter = dd.center;
                }
            }
            return (item, maxradius, bestCenter);
        }

        // Build a local candidate set via KNN from the face centroid.
        // Skip when P is small (k would equal universe size — no restriction possible).
        IList<int> GetLocalCandidates(HashSet<int> universe)
        {
            int k = Math.Min(Dimensions * 12, universe.Count);
            if (k >= universe.Count) return null;
            var centroid = GeometryHelpers.Centroid(actual.ToArray(), positions);
            var local = tree.GetNearestNeighbours(centroid, k)
                            .Select(x => x.Value)
                            .Where(universe.Contains)
                            .ToList();
            return local.Count > Dimensions ? local : null;
        }

        // Only attempt local candidates when the pool is larger than K — otherwise
        // the KNN overhead (HashSet construction + tree query) exceeds any savings.
        IList<int> local = null;
        if (UseLocalCandidates && P.Count > Dimensions * 12)
        {
            var universe = new HashSet<int>(P);
            local = GetLocalCandidates(universe);
        }

        IList<int> c = local ?? P;

        OnRaiseTriangulationEvent(new()
        {
            Candidates = c.Select(x => positions[x].Position).ToArray(),
            Transient  = true,
            Type       = TriangulationEventType.Candidates
        });

        var (apex, dd, center) = RunSearch(c);

        // Verify-then-fallback: if a local apex was found, confirm that no non-local
        // universe point sits strictly inside its circumball. If one does, the local
        // result violates the Delaunay condition and we fall back to the full search.
        if (local != null)
        {
            bool needFallback;
            if (apex == null)
            {
                // All local candidates were filtered (coplanar, same side, already completed) —
                // the correct apex must be outside the local set.
                needFallback = true;
            }
            else
            {
                double absRadius = Math.Abs(dd);
                if (absRadius <= Epsilon)
                {
                    // Degenerate circumball — accept the local result as-is.
                    needFallback = false;
                }
                else
                {
                    // Check every non-local, non-face point against the circumball.
                    // O(N−k) cheap distance comparisons — much faster than O(N) full circumball evaluations.
                    double threshold = (absRadius - Epsilon) * (absRadius - Epsilon);
                    var localSet = new HashSet<int>(local);
                    var faceSet  = new HashSet<int>(actual) { apex.Value };
                    needFallback = false;
                    for (int pi = 0; pi < P.Count; pi++)
                    {
                        int p = P[pi];
                        if (!localSet.Contains(p) && !faceSet.Contains(p) &&
                            DistSq(positions[p].Position, center) < threshold)
                        {
                            needFallback = true;
                            break;
                        }
                    }
                }
            }
            if (needFallback)
                (apex, _, _) = RunSearch(P);
        }

        return apex;

        static double DistSq(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) { double d = a[i] - b[i]; s += d * d; }
            return s;
        }
    }

    /// <summary>
    /// Computes the signed Delaunay radius of the circumball of <paramref name="face"/> ∪ {<paramref name="point"/>}.
    /// The sign encodes which side of the face the circumball centre falls on:
    /// negative means the centre is on the same side as <paramref name="opposite"/> (the ball has rolled
    /// past the face — this candidate is preferred), positive means the other side.
    /// </summary>
    /// <param name="face">Current open face vertex indices.</param>
    /// <param name="opposite">The vertex on the already-filled side (may be <c>null</c> during seed construction).</param>
    /// <param name="point">Apex candidate index.</param>
    /// <param name="positions">Full position list.</param>
    /// <param name="fc">Precomputed face data for the fast LU path; <c>null</c> triggers the Cayley-Menger fallback.</param>
    (double dd, double[] center) DelauneyDistance(IList<int> face, int? opposite, int point, IList<TVertex> positions, FaceCircumsphere? fc = null)
    {
        var r = fc.HasValue
            ? MiniBaller.CircumsphereWithApex(fc.Value, positions[point].Position)
            : MiniBaller.MiniBall((face.Append(point)).ToArray(), positions);

        if (r.radius.IsFinite())
        {
            OnRaiseTriangulationEvent(new()
            {
                Type      = TriangulationEventType.Ball,
                Transient = true,
                Center    = r.center,
                Radius    = r.radius
            });
        }

        if (opposite != null)
        {
            var normalandcentroid = Normalizator.NormalAndCentroid(face.ToArray(), positions);
            // PointSame returns true when the circumcenter is on the same side as `opposite`.
            // That means the ball has pivoted to the correct side — use a negative radius
            // so this candidate is ranked ahead of same-side balls in RunSearch's min comparison.
            var isinhalfspace = GeometryHelpers.PointSame(
                normalandcentroid.Normal, normalandcentroid.Centroid,
                r.center, positions[opposite.Value].Position, Epsilon);
            var radius = isinhalfspace ? -r.radius : r.radius;
            return (radius, r.center);
        }
        return (r.radius, r.center);
    }

    /// <summary>
    /// Grows a new simplex from <paramref name="f"/> by calling <see cref="NextPoint"/>.
    /// Returns <c>null</c> when no valid apex exists.
    /// </summary>
    IList<int> MakeSimplex(IList<int> f, IList<int> P, IList<TVertex> positions, int opposite)
    {
        OnRaiseTriangulationEvent(new TriangulationEventArgs()
        {
            Face      = f.Select(x => Positions[x].Position).ToArray(),
            Transient = true,
            Type      = TriangulationEventType.Face
        });
        var i = NextPoint(f, positions, P, opposite);
        if (i == null) return null;
        return new List<int>(f).Append(i.Value).ToList();
    }

    /// <summary>
    /// Constructs the first d+1-simplex that straddles the split plane.
    /// Starts from the point closest to the median on one side, adds the nearest
    /// cross-plane neighbour, then repeatedly calls <see cref="NextPoint"/> to
    /// accumulate the remaining d−1 vertices.
    /// </summary>
    /// <returns>Index list of the seed simplex vertices, or <c>null</c> if no valid simplex exists.</returns>
    IList<int> MakeFirstSimplex(IList<int> P, AlphaPlane alpha, IList<TVertex> positions)
    {
        // Find the point on the right of the median that is closest to it.
        var mindistance = double.PositiveInfinity;
        var imindistance = -1;
        List<int> result = new();
        foreach (var i in Enumerable.Range(0, P.Count))
        {
            try
            {
                var d = positions[P[i]].Position[alpha.SplitDimension] - alpha.Median;
                if (mindistance > d && positions[P[i]].Position[alpha.SplitDimension] > alpha.Median)
                {
                    mindistance  = d;
                    imindistance = i;
                }
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
        }
        var p1 = imindistance;
        result.Add(p1);

        // Add the nearest point on the opposite side of the median.
        var p1medianleft = positions[p1].Position[alpha.SplitDimension] <= alpha.Median;
        foreach (var pp in tree.GetNearestNeighbours(positions[p1].Position, P.Count))
        {
            if (pp.Value == p1) continue;
            if ((positions[pp.Value].Position[alpha.SplitDimension] >  alpha.Median && p1medianleft)
             || (positions[pp.Value].Position[alpha.SplitDimension] <= alpha.Median && !p1medianleft))
            {
                result.Add(pp.Value);
                break;
            }
        }

        // Grow to a full d+1-simplex one vertex at a time.
        int? p3 = null;
        while (result.Count() < Dimensions + 1)
        {
            p3 = NextPoint(result, positions, P);
            if (p3 != null)
                result.Add(p3.Value);
            else
                return null;
        }
        OnRaiseTriangulationEvent(new()
        {
            Type     = TriangulationEventType.FirstTriangle,
            Transient = true,
            Triangle  = result.Select(x => positions[x].Position).ToArray()
        });
        return result;
    }

    /// <summary>
    /// Splits <paramref name="P"/> into two halves at the median of the highest-variance coordinate,
    /// and constructs the corner matrix of the split hyperplane for event rendering.
    /// </summary>
    (List<int> P1, List<int> P2, AlphaPlane alphaplane) PointsetPartition(IList<int> P, IList<TVertex> Positions)
    {
        if ((P?.Count ?? 0) == 0) return (null, null, null);

        // Find the dimension with the highest variance — this gives the most balanced split.
        double maxvariance = 0;
        var dimensions = Dimensions;
        int plane = 0;
        double[] max = new double[dimensions];
        double[] min = new double[dimensions];
        foreach (var i in Enumerable.Range(0, dimensions))
        {
            var variance = Statistics.Variance(P.Select(x => Positions[x].Position[i]));
            max[i] = Statistics.Maximum(P.Select(x => Positions[x].Position[i]));
            min[i] = Statistics.Minimum(P.Select(x => Positions[x].Position[i]));
            if (maxvariance < variance) { maxvariance = variance; plane = i; }
        }

        var median = Statistics.Median(P.Select(x => Positions[x].Position[plane]));

        // Avoid degenerate splits where all points land on one side.
        if (P.All(x => Positions[x].Position[plane] <= median))
            median = P.Select(x => Positions[x].Position[plane]).Min();
        if (P.All(x => Positions[x].Position[plane] > median))
            median = P.Select(x => Positions[x].Position[plane]).OrderBy(x => x).SkipLast(1).Last();

        // Build the corner matrix of the split hyperplane (used only for rendering).
        Matrix<double> alpha = Matrix<double>.Build.DenseOfRows(
            Enumerable.Repeat(
                Vector<double>.Build.DenseOfEnumerable(Enumerable.Repeat(median, dimensions)),
                dimensions));
        foreach (var i in Enumerable.Range(0, dimensions))
        {
            var col = 0;
            for (int j = 0; j < dimensions; j++)
            {
                if (j == plane) continue;
                if ((i >> col) % 2 == 0) alpha[i, j] = max[j];
                else                     alpha[i, j] = min[j];
                col++;
            }
        }

        return (P.Where(x => Positions[x].Position[plane] <= median).ToList(),
                P.Where(x => Positions[x].Position[plane] >  median).ToList(),
                new(Epsilon) { Plane = alpha, Median = median, SplitDimension = plane });
    }
}

/// <summary>Concrete <see cref="ITriangulation{TVertex,TCell}"/> produced by <see cref="DelauneyTriangulator{TVertex,TFace}"/>.</summary>
public class Triangulation<TVertex, TCell> : ITriangulation<TVertex, TCell>
        where TCell : TriangulationCell<TVertex, TCell>, new()
        where TVertex : IVertex
{
    internal List<TCell> Faces = new List<TCell>();
    /// <inheritdoc/>
    public IEnumerable<TCell> Cells => Faces;
}

/// <summary>Identifies the type of step reported by a <see cref="TriangulationEventArgs"/>.</summary>
public enum TriangulationEventType
{
    /// <summary>An open face is being processed.</summary>
    Face,
    /// <summary>A circumball is being evaluated.</summary>
    Ball,
    /// <summary>A simplex has been accepted (transient = candidate, non-transient = final).</summary>
    Triangle,
    /// <summary>The partition split plane.</summary>
    Plane,
    /// <summary>The seed simplex.</summary>
    FirstTriangle,
    /// <summary>An edge (currently unused by the visualiser).</summary>
    Line,
    /// <summary>A locally-restricted candidate simplex.</summary>
    LocalTriangle,
    /// <summary>The set of local apex candidates for the current face.</summary>
    Candidates
}

/// <summary>
/// Payload for a single triangulation step event. Which properties are populated depends
/// on <see cref="Type"/>: e.g. <c>Ball</c> sets <see cref="Center"/> and <see cref="Radius"/>;
/// <c>Triangle</c> sets <see cref="Triangle"/>; <c>Plane</c> sets <see cref="Plane"/>.
/// </summary>
public class TriangulationEventArgs : EventArgs
{
    /// <inheritdoc cref="TriangulationEventType"/>
    public TriangulationEventType Type { get; set; }
    /// <summary>Candidate apex positions for the current face (Candidates event).</summary>
    public double[][] Candidates { get; set; }
    /// <summary>Vertex positions of a simplex (Triangle / FirstTriangle events).</summary>
    public double[][] Triangle { get; set; }
    /// <summary>Endpoints of an edge (Line event).</summary>
    public double[][] Line { get; set; }
    /// <summary>Corner matrix of the split hyperplane (Plane event).</summary>
    public double[][] Plane { get; set; }
    /// <summary>Circumball centre (Ball event).</summary>
    public double[] Center { get; set; }
    /// <summary>Circumball radius (Ball event).</summary>
    public double Radius { get; set; }
    /// <summary>Face vertex positions (Face event).</summary>
    public double[][] Face { get; set; }
    /// <summary>
    /// When <c>true</c> the visualiser should replace any previous event of the same type
    /// (e.g. rolling circumball). When <c>false</c> the geometry is permanent.
    /// </summary>
    public bool Transient { get; set; }
    /// <summary>When <c>true</c> all previous events of the same type should be erased first.</summary>
    public bool ErasePrevious { get; set; } = false;
}

/// <summary>
/// An ordered set of (face, opposite-vertex) pairs representing open faces awaiting an apex.
/// <see cref="Update"/> implements a toggle: adding a face that is already present removes it
/// (both sides are now filled and the face is interior), while adding a new face enqueues it.
/// </summary>
class FaceList : IEnumerable<(List<int> Face, int Opposite)>
{
    OrderedSet<SimplexComparableWithOpposite> Faces = new(new SimplexComparer<SimplexComparableWithOpposite>());

    public FaceList() { }
    public FaceList(params (List<int> Face, int Opposite)[] faces)
    {
        foreach (var x in faces) Add(x);
    }

    /// <summary>Adds a face unconditionally.</summary>
    public void Add((List<int> Face, int Opposite) face) =>
        Faces.Add(new SimplexComparableWithOpposite(face.Face, face.Opposite));

    /// <summary>Number of pending open faces.</summary>
    public int Count { get => Faces.Count; }

    /// <summary>Removes the oldest pending face (FIFO).</summary>
    public void RemoveFirst() => Faces.RemoveFirst();

    /// <summary>
    /// Toggles the face: removes it if already present (the face is now interior),
    /// otherwise adds it as a new open face.
    /// </summary>
    public void Update((List<int> Face, int Opposite) face)
    {
        var sc = new SimplexComparableWithOpposite(face.Face, face.Opposite);
        if (Faces.Contains(sc)) { Faces.Remove(sc); return; }
        Faces.Add(sc);
    }

    /// <inheritdoc/>
    public IEnumerator<(List<int> Face, int Opposite)> GetEnumerator() =>
        Faces.Select(x => (x.Face, x.Opposite)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        Faces.Select(x => (x.Face, x.Opposite)).GetEnumerator();
}
