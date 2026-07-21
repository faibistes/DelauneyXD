using Delauney.Triangulation;
using Delauney.Triangulation.Core;
using MathNet.Numerics.LinearAlgebra;
using static Delauney.Util.Combinations;
using static Delauney.Util.DataExtensions;

namespace Delauney.Learner;

/// <summary>
/// A triangulation simplex (triangle in 2D, tetrahedron in 3D, n-simplex in nD)
/// composed of <see cref="Vertex"/> instances. Adds geometric queries needed by
/// the classifier: aspect ratio, distance to a query point, and centroid.
/// </summary>
public class Cell : TriangulationCell<Vertex, Cell>
{
    private double EdgeLength(Vertex p1, Vertex p2) =>
        (p2.AsVector() - p1.AsVector()).L2Norm();

    private double? _aspectRatio = null;

    /// <summary>
    /// Ratio of the longest to the shortest edge of this simplex.
    /// A value close to 1 indicates a well-shaped (near-regular) simplex;
    /// very large values indicate sliver simplices near the convex hull boundary.
    /// Result is cached after the first call.
    /// </summary>
    public double AspectRatio()
    {
        if (_aspectRatio != null) return _aspectRatio.Value;
        var edgeLengths = Vertices.ToArray().GetDifferentCombinations(2)
            .Select(x => EdgeLength(x.First(), x.Last()));
        var max = edgeLengths.Max();
        var min = edgeLengths.Min();
        _aspectRatio = min > 0 ? max / min : double.PositiveInfinity;
        return _aspectRatio.Value;
    }

    /// <summary>Creates a cell from an explicit vertex array.</summary>
    public Cell(Vertex[] v) => Vertices = v;

    /// <summary>
    /// Orthogonal distance from <paramref name="point"/> to the nearest point on or inside
    /// this simplex, together with that nearest point.
    /// Returns <c>(∞, null)</c> if the projection fails numerically.
    /// </summary>
    /// <param name="point">Query point in the same R^d as the cell's vertices.</param>
    public (double Distance, Vertex Projection) Distance(Vertex point)
    {
        try
        {
            var d = Distances.Distance.DistanceToSimplex(
                point.AsVector(),
                Matrix<double>.Build.DenseOfRowVectors(Vertices.Select(x => ((Vertex)x).AsVector())));
            return (d.distance, new Vertex(d.projection.AsArray()));
        }
        catch
        {
            return (double.PositiveInfinity, null);
        }
    }

    /// <summary>
    /// The class label of this cell, taken from its first vertex.
    /// All vertices in a cell share the same label by construction (each label's
    /// points are triangulated independently).
    /// </summary>
    public string Label => Vertices.First().Label;

    /// <inheritdoc/>
    public override string ToString() =>
        $"({string.Join(",", Vertices.Select(x => x.ToString()))})";

    /// <summary>Parameterless constructor required by the triangulation framework.</summary>
    public Cell() : base() { }

    /// <summary>
    /// Arithmetic mean of all vertex coordinates — used as the KD-tree key when
    /// building the nearest-simplex index.
    /// </summary>
    public double[] Centroid() =>
        Enumerable.Range(0, Vertices.First().Count())
            .Select(i => (double)Vertices.Average(x => x.AsArray()[i]))
            .ToArray();
}
