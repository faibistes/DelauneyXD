using Delauney.Distances;
using Delauney.Learner;
using Delauney.Triangulation;
using Delauney.Triangulation.Core;
using MathNet.Numerics.LinearAlgebra;
using Xunit;

namespace Delauney.Tests;

public class AspectRatioTests
{
    [Fact]
    public void Equilateral_triangle_returns_one()
    {
        // All edges equal → max/min = 1
        var v0 = new Vertex(new[] { 0.0, 0.0 });
        var v1 = new Vertex(new[] { 1.0, 0.0 });
        var v2 = new Vertex(new[] { 0.5, Math.Sqrt(3) / 2 });
        var cell = new Cell(new[] { v0, v1, v2 });
        Assert.Equal(1.0, cell.AspectRatio(), 6);
    }

    [Fact]
    public void Degenerate_cell_returns_positive_infinity()
    {
        // Collinear vertices → min edge effectively 0
        var v0 = new Vertex(new[] { 0.0, 0.0 });
        var v1 = new Vertex(new[] { 0.0, 0.0 });
        var v2 = new Vertex(new[] { 1.0, 0.0 });
        var cell = new Cell(new[] { v0, v1, v2 });
        Assert.Equal(double.PositiveInfinity, cell.AspectRatio());
    }

    [Fact]
    public void Elongated_triangle_ratio_greater_than_one()
    {
        var v0 = new Vertex(new[] { 0.0, 0.0 });
        var v1 = new Vertex(new[] { 10.0, 0.0 });
        var v2 = new Vertex(new[] { 5.0, 0.01 });
        var cell = new Cell(new[] { v0, v1, v2 });
        Assert.True(cell.AspectRatio() > 1.0);
    }
}

public class DistanceToSimplexTests
{
    static Vector<double> Vec(params double[] v) => Vector<double>.Build.DenseOfArray(v);
    static Matrix<double> Mat(params double[][] rows) => Matrix<double>.Build.DenseOfRowVectors(rows.Select(Vec).ToArray());

    [Fact]
    public void Point_at_vertex_returns_zero()
    {
        var S = Mat(new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 });
        var (d, _) = Distance.DistanceToSimplex(Vec(0, 0), S);
        Assert.Equal(0.0, d, 9);
    }

    [Fact]
    public void Point_inside_returns_zero_distance()
    {
        var S = Mat(new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 });
        var (d, _) = Distance.DistanceToSimplex(Vec(0.25, 0.25), S);
        Assert.Equal(0.0, d, 6);
    }

    [Fact]
    public void Point_outside_returns_positive_distance()
    {
        var S = Mat(new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 });
        var (d, _) = Distance.DistanceToSimplex(Vec(2.0, 0.0), S);
        Assert.True(d > 0);
        Assert.Equal(1.0, d, 6);
    }

    [Fact]
    public void Single_point_simplex_returns_euclidean_distance()
    {
        var S = Mat(new[] { 3.0, 4.0 });
        var (d, _) = Distance.DistanceToSimplex(Vec(0, 0), S);
        Assert.Equal(5.0, d, 6);
    }
}

public class TriangulationTests
{
    [Fact]
    public void Four_points_in_2D_produces_triangles()
    {
        var points = new List<Vertex>
        {
            new("A", new[] { 0.0, 0.0 }),
            new("A", new[] { 1.0, 0.0 }),
            new("A", new[] { 0.0, 1.0 }),
            new("A", new[] { 1.0, 1.0 }),
        };
        var t = new DelauneyTriangulator<Vertex, Cell>();
        var result = t.CreateDelaunay(points);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Cells);
        // Each cell is a triangle (3 vertices in 2D)
        Assert.All(result.Cells, c => Assert.Equal(3, c.Vertices.Length));
    }

    [Fact]
    public async Task Classifier_two_classes_2D_achieves_perfect_accuracy_on_separated_blobs()
    {
        var rng = new Random(42);
        double Gauss()
        {
            double u = 1 - rng.NextDouble(), v = 1 - rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
        }

        var trainA = Enumerable.Range(0, 20).Select(_ => new Vertex("A", new[] { Gauss() - 5, Gauss() })).ToList<Vertex>();
        var trainB = Enumerable.Range(0, 20).Select(_ => new Vertex("B", new[] { Gauss() + 5, Gauss() })).ToList<Vertex>();
        var testA  = Enumerable.Range(0, 10).Select(_ => new[] { Gauss() - 5, Gauss() }).ToArray();
        var testB  = Enumerable.Range(0, 10).Select(_ => new[] { Gauss() + 5, Gauss() }).ToArray();

        var data = new Dictionary<string, IList<Vertex>> { ["A"] = trainA, ["B"] = trainB };
        var learner = new Learner.Learner(data);
        await learner.BuildTriangulations();
        await learner.MakeIndex(double.MaxValue);

        var pA = await learner.Predict(testA, 5);
        var pB = await learner.Predict(testB, 5);

        Assert.All(pA, label => Assert.Equal("A", label));
        Assert.All(pB, label => Assert.Equal("B", label));
    }
}
