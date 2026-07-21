using MathNet.Numerics.LinearAlgebra;

namespace Delauney.Learner;

/// <summary>
/// Extension methods for collections of <see cref="Cell"/> simplices.
/// </summary>
public static class CellExtensions
{
    /// <summary>
    /// Retains only simplices whose aspect ratio (longest edge / shortest edge) is at or below
    /// <paramref name="fraction"/>. Lower values keep only well-shaped simplices;
    /// higher values admit elongated boundary-tracing ones.
    /// Use this after triangulation to produce an alpha-shape–style surface.
    /// </summary>
    /// <param name="cells">Source simplex collection.</param>
    /// <param name="fraction">Maximum allowed aspect ratio.</param>
    public static IEnumerable<Cell> FilterSimplices(this IEnumerable<Cell> cells, double fraction) =>
        cells.Where(x => x.AspectRatio() <= fraction);
}
