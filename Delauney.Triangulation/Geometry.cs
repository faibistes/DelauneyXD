using System.Collections.Concurrent;
using Delauney.Distances;
using Delauney.Triangulation.Core;
using MathNet.Numerics.LinearAlgebra;

namespace Delauney.Triangulation;

internal static class GeometryHelpers
{
    /// <summary>
    /// Arithmetic centroid of the given vertex indices in the position list.
    /// Returns a fresh coordinate array of length d.
    /// </summary>
    public static double[] Centroid<TVertex>(int[] vertices, IList<TVertex> positions)
        where TVertex : IVertex =>
        Enumerable.Range(0, positions[0].Position.Length)
            .Select(i => vertices.Select(x => positions[x].Position[i]).Average())
            .ToArray();

    /// <summary>
    /// Returns <c>true</c> when <paramref name="point"/> and <paramref name="other"/> lie on
    /// the same side of the hyperplane defined by <paramref name="normal"/> through
    /// <paramref name="centroid"/>. Used to enforce the half-space constraint when growing
    /// simplices away from an already-visited side.
    /// </summary>
    public static bool PointSame(double[] normal, double[] centroid, double[] point, double[] other, double epsilon = 1e-10)
    {
        var n = Vector<double>.Build.DenseOfArray(normal);
        var c = Vector<double>.Build.DenseOfArray(centroid);
        var d1 = n.DotProduct(Vector<double>.Build.DenseOfArray(other) - c);
        var d2 = n.DotProduct(Vector<double>.Build.DenseOfArray(point) - c);
        // Both strictly positive or both strictly negative → same side.
        return (d1 < -epsilon && d2 < -epsilon) || (d1 > epsilon && d2 > epsilon);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="point"/> lies within <paramref name="epsilon"/>
    /// of the hyperplane spanned by <paramref name="normal"/> through <paramref name="centroid"/>.
    /// Points on the plane are excluded from candidate searches to avoid degenerate simplices.
    /// </summary>
    public static bool CoplanarAfin(double[] point, double[] normal, double[] centroid, double epsilon = 1e-10)
    {
        var v = Vector<double>.Build.DenseOfArray(normal).DotProduct(
                    Vector<double>.Build.DenseOfArray(point)
                    - Vector<double>.Build.DenseOfArray(centroid));
        return v <= epsilon && v >= -epsilon;
    }
}

internal class SimplexComparableWithOpposite : SimplexComparable, IEquatable<SimplexComparableWithOpposite>
{
    public SimplexComparableWithOpposite(IEnumerable<int> nodes) : base(nodes) { }
    public SimplexComparableWithOpposite(IEnumerable<int> nodes, int opposite) : this(nodes)
    {
        Opposite = opposite;
    }
    public int Opposite { get; set; }
    public bool Equals(SimplexComparableWithOpposite? other) => base.Equals(other);
}

/// <summary>
/// Generic equality comparer for <see cref="SimplexComparable"/> subclasses,
/// used as the key comparer for <c>HashSet</c> / <c>Dictionary</c> collections of faces.
/// </summary>
internal class SimplexComparer<T> : EqualityComparer<T>
    where T : SimplexComparable
{
    public override bool Equals(T b1, T b2) => b1.Equals(b2);
    public override int GetHashCode(T b1) => b1.GetHashCode();
}

/// <summary>
/// An unordered set of vertex indices representing a simplex face,
/// comparable by content rather than identity. Vertices are kept in a
/// sorted set so that {0,1,2} and {2,0,1} hash and compare as equal.
/// </summary>
internal class SimplexComparable : IEquatable<SimplexComparable>
{
    SortedSet<int> Nodes { get; set; }
    int? hashcode = null;

    public List<int> Face => Nodes.ToList();

    public SimplexComparable(IEnumerable<int> nodes)
    {
        Nodes = new(nodes);
    }

    public override int GetHashCode()
    {
        if (hashcode != null) return hashcode.Value;
        unchecked
        {
            int hash = 19;
            foreach (var n in Nodes)
                hash = hash * 31 + n.GetHashCode();
            hashcode = hash;
            return hash;
        }
    }

    public bool Equals(SimplexComparable other)
    {
        if (other == null) return false;
        if (other.GetHashCode() != GetHashCode()) return false;
        foreach (var x in Nodes.Zip(other.Nodes))
            if (x.First != x.Second) return false;
        return true;
    }
}

/// <summary>
/// Precomputed data for the circumball linear system of a d-face.
/// Building this once per face amortises the repeated row-construction cost
/// across all apex candidates evaluated for that face.
/// </summary>
/// <remarks>
/// The circumcenter c satisfies 2(pᵢ−p₀)·c = |pᵢ|²−|p₀|² for i = 1…d.
/// The d−1 rows corresponding to the face vertices (i = 1…d−1) are constant
/// across candidates and are stored here. The apex contributes the last row.
/// </remarks>
internal readonly struct FaceCircumsphere
{
    /// <summary>Position of the first face vertex (p₀); used as the reference origin.</summary>
    internal readonly double[] P0;
    /// <summary>|P0|² — precomputed to avoid recomputing per candidate.</summary>
    internal readonly double P0Sq;
    /// <summary>(d−1)×d row-major matrix: row i = 2(p_{i+1}−p₀).</summary>
    internal readonly double[] Rows;
    /// <summary>Right-hand side for the d−1 face rows: rhs[i] = |p_{i+1}|²−|p₀|².</summary>
    internal readonly double[] Rhs;
    /// <summary>Ambient dimension d.</summary>
    internal readonly int D;

    internal FaceCircumsphere(double[] p0, double p0sq, double[] rows, double[] rhs, int d)
    { P0 = p0; P0Sq = p0sq; Rows = rows; Rhs = rhs; D = d; }
}

/// <summary>
/// Circumball (circumsphere) computations for arbitrary dimension.
/// Provides two paths:
/// <list type="bullet">
///   <item><b>Fast path</b> (<see cref="CircumsphereWithApex"/>): d×d linear system via LU
///     decomposition, using stack-allocated scratch arrays for zero GC pressure.</item>
///   <item><b>Fallback</b> (<see cref="MiniBall"/>): Fiedler/Cayley-Menger (d+2)×(d+2)
///     determinant formula via MathNet, used when the face matrix is not yet complete
///     (early steps of <c>MakeFirstSimplex</c> in d≥3).</item>
/// </list>
/// </summary>
internal class MiniBaller<TVertex>
    where TVertex : IVertex
{
    public int Dimensions { get; set; }

    public MiniBaller(int dimensions) => Dimensions = dimensions;

    // ── Fallback: Fiedler / Cayley-Menger formula ────────────────────────────
    // Works for any number of points in any dimension. Constructs the (d+2)×(d+2)
    // bordered distance matrix and inverts it to find the circumcenter and radius.

    /// <summary>
    /// Circumball of <paramref name="points"/> via the Fiedler/Cayley-Menger determinant.
    /// Returns <c>(∞, ∞…)</c> when the matrix is singular (degenerate point set).
    /// </summary>
    static (double radius, double[] center) MiroslavFielderMiniBall<TVertex>(params TVertex[] points)
        where TVertex : IVertex
    {
        var v = points.Select(x => x.AsVector()).ToArray();
        var count = points.Length;
        List<Vector<double>> rows = new();
        rows.Add(Vector<double>.Build.DenseOfEnumerable(Enumerable.Repeat(0d, 1).Concat(Enumerable.Repeat(1d, count))));
        foreach (var r in Enumerable.Range(1, count))
        {
            double[] cols = new double[count + 1];
            cols[0] = 1;
            foreach (var c in Enumerable.Range(1, count))
                cols[c] = Math.Pow((v[c - 1] - v[r - 1]).L2Norm(), 2);
            rows.Add(Vector<double>.Build.DenseOfArray(cols));
        }
        var C = Matrix<double>.Build.DenseOfRows(rows);
        Matrix<double> M;
        try { M = (-2) * C.Inverse(); }
        catch { return (double.PositiveInfinity, Enumerable.Repeat(double.PositiveInfinity, v[0].Count).ToArray()); }
        Vector<double> center = Vector<double>.Build.DenseOfEnumerable(Enumerable.Repeat(0d, v[0].Count));
        var div = 0d;
        foreach (var i in Enumerable.Range(0, count))
        { center += M[0, i + 1] * v[i]; div += M[0, i + 1]; }
        center /= div;
        return (Math.Sqrt(M[0, 0]) / 2, center.ToArray());
    }

    /// <summary>
    /// Circumball of a set of point indices via the Cayley-Menger fallback.
    /// Used when the face is not yet full (fewer than d vertices during <c>MakeFirstSimplex</c>).
    /// </summary>
    public (double radius, double[] center) MiniBall(int[] points, IList<TVertex> positions)
        => MiroslavFielderMiniBall(points.Select(x => positions[x]).ToArray());

    // ── Fast path: d×d linear circumcenter system ────────────────────────────

    /// <summary>
    /// Precomputes the d−1 constant rows of the circumball linear system for the given face.
    /// Call once per face; pass the result to <see cref="CircumsphereWithApex"/> for each candidate.
    /// Cost: O(d²).
    /// </summary>
    /// <param name="face">Vertex indices of the d-face (exactly <c>Dimensions</c> entries).</param>
    /// <param name="positions">Full position list.</param>
    public FaceCircumsphere PrecomputeFace(IList<int> face, IList<TVertex> positions)
    {
        int d = Dimensions;
        double[] p0   = positions[face[0]].Position;
        double   p0sq = Dot(p0, p0, d);
        double[] rows = new double[(d - 1) * d];
        double[] rhs  = new double[d - 1];
        for (int i = 0; i < d - 1; i++)
        {
            double[] pi = positions[face[i + 1]].Position;
            rhs[i] = Dot(pi, pi, d) - p0sq;
            int ofs = i * d;
            for (int j = 0; j < d; j++) rows[ofs + j] = 2.0 * (pi[j] - p0[j]);
        }
        return new FaceCircumsphere(p0, p0sq, rows, rhs, d);
    }

    /// <summary>
    /// Completes the circumball system by adding the apex row, then solves via LU decomposition.
    /// A and b live on the stack (stackalloc) — no heap allocation per candidate.
    /// Cost: O(d²) copy + O(d³) LU solve.
    /// </summary>
    /// <param name="fc">Precomputed face data from <see cref="PrecomputeFace"/>.</param>
    /// <param name="apex">Coordinates of the apex candidate.</param>
    /// <returns>
    /// Circumball radius and center. Returns <c>(∞, zero[])</c> when the simplex is
    /// degenerate (LU pivot below threshold).
    /// </returns>
    public (double radius, double[] center) CircumsphereWithApex(in FaceCircumsphere fc, double[] apex)
    {
        int d = fc.D;
        // Stack-allocate scratch arrays to avoid GC pressure in the hot loop.
        Span<double> A = stackalloc double[d * d];
        Span<double> b = stackalloc double[d];

        // Copy the d−1 precomputed face rows, then fill the apex row.
        fc.Rows.AsSpan(0, (d - 1) * d).CopyTo(A);
        fc.Rhs.AsSpan(0, d - 1).CopyTo(b);

        double apsq = Dot(apex, apex, d);
        b[d - 1] = apsq - fc.P0Sq;
        int last = (d - 1) * d;
        for (int j = 0; j < d; j++) A[last + j] = 2.0 * (apex[j] - fc.P0[j]);

        if (!SolveLU(A, b, d))
            return (double.PositiveInfinity, new double[d]);

        // b now holds the circumcenter; compute radius as distance from p₀.
        double r2 = 0;
        for (int j = 0; j < d; j++) { double diff = b[j] - fc.P0[j]; r2 += diff * diff; }
        double[] center = new double[d];
        b.CopyTo(center);
        return (Math.Sqrt(r2), center);
    }

    /// <summary>
    /// Gaussian elimination with partial pivoting. Solves A·x = b in-place; b becomes x on return.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the matrix is numerically singular (max pivot &lt; 1e-14),
    /// which happens for coplanar or coincident point sets.
    /// </returns>
    static bool SolveLU(Span<double> A, Span<double> b, int d)
    {
        for (int k = 0; k < d; k++)
        {
            // Find the largest pivot in column k at or below row k.
            int pivot = k;
            double maxV = Math.Abs(A[k * d + k]);
            for (int i = k + 1; i < d; i++)
            {
                double v = Math.Abs(A[i * d + k]);
                if (v > maxV) { maxV = v; pivot = i; }
            }
            // Singular check: threshold of 1e-14 is tight enough for double precision
            // while still catching genuinely degenerate configurations.
            if (maxV < 1e-14) return false;
            if (pivot != k)
            {
                for (int j = 0; j < d; j++)
                    (A[k * d + j], A[pivot * d + j]) = (A[pivot * d + j], A[k * d + j]);
                (b[k], b[pivot]) = (b[pivot], b[k]);
            }
            double akk = A[k * d + k];
            for (int i = k + 1; i < d; i++)
            {
                double f = A[i * d + k] / akk;
                for (int j = k + 1; j < d; j++) A[i * d + j] -= f * A[k * d + j];
                b[i] -= f * b[k];
                A[i * d + k] = 0;
            }
        }
        // Back-substitution.
        for (int k = d - 1; k >= 0; k--)
        {
            for (int j = k + 1; j < d; j++) b[k] -= A[k * d + j] * b[j];
            b[k] /= A[k * d + k];
        }
        return true;
    }

    static double Dot(double[] a, double[] b, int d)
    { double s = 0; for (int i = 0; i < d; i++) s += a[i] * b[i]; return s; }
}

/// <summary>
/// Caches face normals and centroids computed via SVD. The cache is a
/// <see cref="ConcurrentDictionary"/> so it is safe to read and write from the
/// parallel recursive sub-problems. <c>GetOrAdd</c> may compute the SVD twice
/// under contention, but results are deterministic so this is harmless.
/// </summary>
internal class Normalizator<TVertex>
    where TVertex : IVertex
{
    public int Dimensions { get; set; }

    ConcurrentDictionary<SimplexComparable, (SimplexComparable SC, double[] Normal, double[] Centroid)> cache
        = new(new SimplexComparer<SimplexComparable>());

    public Normalizator(int dimensions) { Dimensions = dimensions; }

    /// <summary>
    /// Returns the unit normal and centroid of the hyperplane spanned by <paramref name="vertices"/>.
    /// The normal is the last right-singular vector of the centred vertex matrix (SVD),
    /// normalised to unit length.
    /// </summary>
    /// <param name="vertices">Vertex indices defining the face.</param>
    /// <param name="positions">Full position list.</param>
    public (double[] Normal, double[] Centroid) NormalAndCentroid(int[] vertices, IList<TVertex> positions)
    {
        var sc = new SimplexComparable(vertices);
        var entry = cache.GetOrAdd(sc, _ =>
        {
            var normal   = new double[Dimensions];
            var centroid = GeometryHelpers.Centroid(vertices, positions);
            // Centre the vertex matrix and take the last right-singular vector —
            // this is the direction of least variance, i.e. the face normal.
            var M2 = Matrix<double>.Build.DenseOfRowVectors(
                vertices.Select(x => positions[x].AsVector() - Vector<double>.Build.DenseOfArray(centroid)));
            var svd = M2.Svd(true);
            var v   = svd.VT.Row(svd.VT.RowCount - 1);
            var ff  = 1.0 / v.L2Norm();
            for (int i = 0; i < Dimensions; i++) normal[i] = v[i] * ff;
            return (sc, normal, centroid);
        });
        return (entry.Normal, entry.Centroid);
    }
}
