# n-Dimensional Delaunay Triangulation

![Delauney.gif](Delauney.gif)

A C# implementation of Delaunay triangulation that works in any number of dimensions.
The core library produces a simplicial complex — in 2D a triangle mesh, in 3D a tetrahedral mesh, in nD a collection of n-simplices — with the Delaunay property: the circumball of every simplex contains no other input point.

## What it does

Given a set of points in R^n, the triangulator:

1. **Partitions** the point set at the median of the highest-variance axis (divide-and-conquer)
2. **Seeds** an initial simplex straddling the split plane by KNN-bootstrapping d+1 points
3. **Stitches** the split-plane boundary by ball-pivoting: for each open face, finds the point whose circumball with that face is minimal and empty
4. **Recurses in parallel** on each half-partition — the two sub-problems are fully independent after the boundary pass and run concurrently via `Parallel.Invoke`

## Circumball computation

Two paths, selected automatically:

- **Fast path** (hot loop): solves the d×d linear system 2(pᵢ−p₀)·c = |pᵢ|²−|p₀|² via Gaussian elimination with partial pivoting. The d−1 face rows are precomputed once per face; only the apex row changes per candidate. Scratch arrays live on the stack (`stackalloc`) — zero GC allocation per candidate.
- **Fallback** (seed construction only): Fiedler/Cayley-Menger (d+2)×(d+2) determinant via MathNet matrix inversion, used when the face has fewer than d vertices during the first-simplex bootstrap.

## Candidate restriction (`UseLocalCandidates`)

For each open face, the apex search can be restricted to the K = Dimensions × 12 nearest neighbours of the face centroid via a KD-tree query (enabled by default).

After selecting the best local apex, the algorithm verifies correctness with an O(N−K) linear distance scan. If any non-local point sits strictly inside the circumball, it falls back to a full O(N) search — so the Delaunay property is guaranteed in any dimension. In practice, fallback is rare; observed scaling is near O(N log N) at large N.

Disable with `UseLocalCandidates = false` for small point sets (a few dozen points per partition) where the KNN overhead exceeds the search savings.

## Performance

Measured on 4 cores (parallel recursion enabled):

| d   | N    | local (ms) | brute (ms) | speedup |
| --- | ---- | ---------- | ---------- | ------- |
| 2   | 300  | ~120       | ~200       | ~1.7×   |
| 2   | 600  | ~230       | ~640       | ~2.8×   |
| 2   | 1000 | ~380       | ~560       | ~1.5×   |
| 3   | 60   | ~35        | ~44        | ~1.3×   |
| 3   | 100  | ~77        | ~97        | ~1.3×   |

Local-candidates scaling from N=300→1000 in 2D is roughly linear (N=300: 120ms → N=1000: 380ms ≈ 3.2× for 3.3× more points).

## Alpha shapes / surface reconstruction

The raw triangulation fills the convex hull. For surface reconstruction and concave boundary detection, elongated simplices can be pruned with `FilterSimplices(aspectRatio)`, where aspect ratio = max\_edge / min\_edge. Lower thresholds keep only well-shaped (near-regular) simplices.

```csharp
var triangulator = new DelauneyTriangulator<Vertex, Cell>();
var triangulation = triangulator.CreateDelaunay(points);

// Keep only compact simplices (alpha-shape style pruning)
var surface = triangulation.Cells.FilterSimplices(factor: 2.0).ToList();
```

## Classifier usage

`Delauney.Learner` wraps the triangulator into a geometric nearest-simplex classifier:

```csharp
var learner = new Learner(labelledDataByClass);
await learner.BuildTriangulations();   // one triangulation per class, in parallel
await learner.MakeIndex(concaveFactor: 2.0);
string predicted = await learner.Predict(queryPoint, k: 5);
```

Prediction finds the k simplex centroids nearest to the query (KD-tree), then returns the label of the simplex with the smallest orthogonal distance to the query point.

## Architecture

```
Delauney.Triangulation   — core triangulator, geometry helpers, circumball solver
Delauney.Distances       — point-to-simplex distance via barycentric projection
Delauney.Learner         — geometric nearest-simplex classifier
Delauney.Util            — combinatorics and dataset split helpers
Delauney.Tests           — unit tests (xUnit)
Delauney.Pruebas         — runnable samples and performance benchmarks
Delauney.Forms           — WinForms step-by-step visualiser (2D)
```

## Build

Requires .NET 8 or later.

```bash
dotnet build Delauney.sln
dotnet test Delauney.Tests/Delauney.Tests.csproj
```

## Visualiser

`Delauney.Forms` animates the triangulation step by step in 2D — circumcircles rolling across open faces, the partition plane, candidate sets highlighted in red, and final simplices drawn in black. Run from Visual Studio or:

```bash
cd Delauney.Forms
dotnet run
```

## Samples

`Delauney.Pruebas` contains runnable examples including a performance benchmark comparing local vs brute-force search across dimensions and sizes. Place `iris.data` (UCI Iris) and optionally `pb.csv` (Peterson & Barney vowel formants) next to the project before running.

```bash
cd Delauney.Pruebas
dotnet run -c Release
```

## Limitations

- **Simplex explosion** — simplex count grows as O(N^⌈d/2⌉) (upper bound theorem). Practical use tops out around d = 4–5 before the output becomes unmanageable.
- **Convex hull** — the triangulation fills the convex hull of the input; concave regions and holes require post-hoc alpha pruning.
- **Parallelism** — each independent class triangulation runs on its own thread (`BuildTriangulations`). Within a single triangulation, the two recursive sub-problems run in parallel (`Parallel.Invoke`) after the boundary stitching pass. The boundary pass itself is sequential (faces depend on each other).
- **Visualiser** — `Delauney.Forms` disables parallel recursion automatically (`WithEvents = true`) since WinForms event callbacks are not thread-safe.
