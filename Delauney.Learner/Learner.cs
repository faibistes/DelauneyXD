using KdTree.Math;
using MathNet.Numerics.LinearAlgebra;
using System.Collections.Concurrent;
using Delauney.Triangulation.Core;
using Delauney.Triangulation;

namespace Delauney.Learner
{
    /// <summary>
    /// Geometric nearest-simplex classifier. For each class label it builds a Delaunay
    /// triangulation of that class's training points, then at prediction time finds the
    /// simplex nearest to the query point and returns its label.
    /// </summary>
    /// <remarks>
    /// Workflow:
    /// <list type="number">
    ///   <item>Construct with a label → points dictionary.</item>
    ///   <item>Call <see cref="BuildTriangulations"/> to triangulate each class in parallel.</item>
    ///   <item>Call <see cref="MakeIndex"/> to build the KD-tree over simplex centroids.</item>
    ///   <item>Call <see cref="Predict(double[], int)"/> or <see cref="Predict(double[][], int)"/>.</item>
    /// </list>
    /// </remarks>
    public class Learner
    {
        private readonly IDictionary<string, IList<Vertex>> _dataset;
        private Dictionary<string, ITriangulation<Vertex, Cell>> triangulations = new();

        // KD-tree keyed by simplex centroid, used to find candidate simplices quickly.
        private KdTree.KdTree<double, Cell> tree;

        /// <summary>Aspect-ratio threshold used when building the index (set by <see cref="MakeIndex"/>).</summary>
        public double PruneFactor { get; private set; }

        /// <summary>
        /// Creates a learner for the given labelled dataset.
        /// </summary>
        /// <param name="data">Map from class label to list of training vertices.</param>
        public Learner(IDictionary<string, IList<Vertex>> data)
        {
            _dataset = data;
            tree = new(data.First().Value.First().Count(), new DoubleMath(), KdTree.AddDuplicateBehavior.Skip);
        }

        /// <summary>
        /// Triangulates each class's point set in parallel. Must be called before
        /// <see cref="MakeIndex"/>. Each class is triangulated independently on its
        /// own thread so classes never share computation.
        /// </summary>
        public async Task BuildTriangulations()
        {
            var results = new ConcurrentDictionary<string, ITriangulation<Vertex, Cell>>();
            await Parallel.ForEachAsync(_dataset.Keys, async (label, _) =>
            {
                var t = new DelauneyTriangulator<Vertex, Cell>();
                var tri = await Task.Run(() => t.CreateDelaunay(_dataset[label]));
                results[label] = tri;
            });
            foreach (var kv in results)
                triangulations[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Builds a KD-tree over the centroids of all simplices that survive the aspect-ratio
        /// filter. Call after <see cref="BuildTriangulations"/>.
        /// </summary>
        /// <param name="concaveFactor">
        /// Maximum aspect ratio to admit (longest / shortest edge). Lower values keep
        /// only compact simplices; higher values include elongated boundary ones.
        /// </param>
        public async Task MakeIndex(double concaveFactor)
        {
            tree.Clear();
            foreach (var x in triangulations.Values)
            {
                foreach (var v in x.Cells.FilterSimplices(concaveFactor))
                {
                    tree.Add(v.Centroid(), v);
                }
            }
        }

        /// <summary>
        /// Classifies a single query point by finding the nearest <paramref name="k"/> simplex
        /// centroids in the index, computing the exact orthogonal distance to each candidate
        /// simplex, and returning the label of the closest one.
        /// </summary>
        /// <param name="v">Query coordinates.</param>
        /// <param name="k">Number of candidate simplices to evaluate (higher = more accurate, slower).</param>
        /// <returns>Predicted class label, or <c>null</c> if no simplex could be evaluated.</returns>
        public async Task<string> Predict(double[] v, int k)
        {
            Vertex vv = new Vertex(v);
            var n = tree.GetNearestNeighbours(v, k);
            var min = double.PositiveInfinity;
            string bestLabel = null;
            foreach (var x in n)
            {
                double dist = double.PositiveInfinity;
                try
                {
                    dist = x.Value.Distance(vv).Distance;
                }
                catch (Exception e)
                {
                    await Console.Out.WriteLineAsync();
                }
                if (dist < min)
                {
                    min = dist;
                    bestLabel = x.Value.Label;
                }
            }
            return bestLabel;
        }

        /// <summary>
        /// Classifies a batch of query points in parallel.
        /// </summary>
        /// <param name="v">Array of query coordinate arrays.</param>
        /// <param name="k">Number of candidate simplices per query.</param>
        /// <returns>Predicted labels in the same order as <paramref name="v"/>.</returns>
        public async Task<string[]> Predict(double[][] v, int k)
        {
            ParallelOptions o = new() { MaxDegreeOfParallelism = 8 };
            string[] result = new string[v.Length];
            ConcurrentDictionary<int, string> d = new();
            await Parallel.ForEachAsync(v.Select((x, i) => (x, i)), o, async (x, _) =>
            {
                var r = await Task.Run(() => Predict(x.x, k));
                d.TryAdd(x.i, r);
            });
            foreach (var x in d)
                result[x.Key] = x.Value;
            return result;
        }
    }
}
