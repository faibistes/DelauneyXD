using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delauney.Triangulation.Core;

/// <summary>
/// Base class for a face of a convex polytope or triangulation simplex.
/// Carries the vertices, optional adjacency links, and optional outward normal.
/// </summary>
/// <typeparam name="TVertex">Vertex type implementing <see cref="IVertex"/>.</typeparam>
/// <typeparam name="TFace">Concrete face type (CRTP self-reference).</typeparam>
public abstract class ConvexFace<TVertex, TFace>
where TVertex : IVertex
where TFace : ConvexFace<TVertex, TFace>
{
    /// <summary>
    /// Adjacent faces, one per vertex: <c>Adjacency[i]</c> shares all vertices except <c>Vertices[i]</c>.
    /// <c>null</c> entries indicate boundary faces (no neighbour on that side).
    /// </summary>
    public TFace[] Adjacency { get; set; }

    /// <summary>
    /// Vertices of this face. In 2-4 dimensions they are stored in clockwise order;
    /// in higher dimensions the order is arbitrary but consistent within a triangulation.
    /// </summary>
    public TVertex[] Vertices { get; set; }

    /// <summary>
    /// Outward unit normal. Populated by convex-hull algorithms; <c>null</c> when used
    /// purely as a triangulation simplex.
    /// </summary>
    public double[] Normal { get; set; }
}

/// <summary>
/// Concrete convex face with no additional data beyond the base class.
/// Suitable for general convex-hull output.
/// </summary>
/// <typeparam name="TVertex">Vertex type implementing <see cref="IVertex"/>.</typeparam>
public class DefaultConvexFace<TVertex> : ConvexFace<TVertex, DefaultConvexFace<TVertex>>
    where TVertex : IVertex
{
}

/// <summary>
/// Contract for a point in R^d. Implementations supply a coordinate array and
/// a MathNet vector view over the same data.
/// </summary>
public interface IVertex
{
    /// <summary>Coordinates in R^d.</summary>
    double[] Position { get; }

    /// <summary>MathNet vector view of <see cref="Position"/>.</summary>
    Vector<double> AsVector();
}

/// <summary>
/// Minimal <see cref="IVertex"/> implementation backed by a plain coordinate array.
/// </summary>
public class DefaultVertex : IVertex
{
    /// <inheritdoc/>
    public double[] Position { get; set; }

    /// <inheritdoc/>
    public Vector<double> AsVector() => Vector<double>.Build.DenseOfArray(Position);
}

/// <summary>
/// Base class for a triangulation simplex (triangle in 2D, tetrahedron in 3D, n-simplex in nD).
/// Extends <see cref="ConvexFace{TVertex,TCell}"/> with no additional members; the distinction
/// is semantic — triangulation cells don't carry convex-hull normals.
/// </summary>
/// <typeparam name="TVertex">Vertex type.</typeparam>
/// <typeparam name="TCell">Concrete cell type (CRTP self-reference).</typeparam>
public abstract class TriangulationCell<TVertex, TCell> : ConvexFace<TVertex, TCell>
where TVertex : IVertex
where TCell : ConvexFace<TVertex, TCell>
{
}

/// <summary>
/// Concrete triangulation cell with no additional data.
/// </summary>
/// <typeparam name="TVertex">Vertex type.</typeparam>
public class DefaultTriangulationCell<TVertex> : TriangulationCell<TVertex, DefaultTriangulationCell<TVertex>>
    where TVertex : IVertex
{
}

/// <summary>
/// Read-only view of a completed triangulation.
/// </summary>
/// <typeparam name="TVertex">Vertex type.</typeparam>
/// <typeparam name="TCell">Cell type.</typeparam>
public interface ITriangulation<TVertex, TCell>
    where TCell : TriangulationCell<TVertex, TCell>, new()
    where TVertex : IVertex
{
    /// <summary>
    /// The simplices that make up the triangulation.
    /// In 2D these are triangles, in 3D tetrahedra, in nD n-simplices.
    /// </summary>
    IEnumerable<TCell> Cells { get; }
}
