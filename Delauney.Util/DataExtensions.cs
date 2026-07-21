namespace Delauney.Util;

/// <summary>
/// General-purpose collection utilities for dataset preparation.
/// </summary>
public static class DataExtensions
{
    /// <summary>
    /// Randomly splits a sequence into a training set and a test set.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="l">Source sequence.</param>
    /// <param name="factor">
    /// Fraction of elements to place in the <em>test</em> set (e.g. <c>0.2</c> for an 80/20 split).
    /// </param>
    /// <param name="seed">
    /// Optional RNG seed for reproducibility. A random seed is chosen when <c>null</c>.
    /// </param>
    /// <returns>
    /// A tuple of (<c>Train</c>, <c>Test</c>) lists. Elements are assigned independently
    /// with probability <paramref name="factor"/> going to the test set.
    /// </returns>
    public static (IList<T> Train, IList<T> Test) Split<T>(this IEnumerable<T> l, double factor, int? seed = null)
    {
        (List<T> Train, List<T> Test) = (new(), new());
        if (seed == null) seed = Guid.NewGuid().GetHashCode();
        var random = new Random(seed.Value);
        foreach (var x in l)
        {
            if (random.NextDouble() < factor)
                Test.Add(x);
            else
                Train.Add(x);
        }
        return (Train, Test);
    }
}
