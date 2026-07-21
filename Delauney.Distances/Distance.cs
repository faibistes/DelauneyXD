using MathNet.Numerics.LinearAlgebra;

namespace Delauney.Distances
{
    /// <summary>
    /// Point-to-simplex distance in R^d via orthogonal projection and barycentric coordinates.
    /// All methods work in arbitrary dimension.
    /// </summary>
    public static class Distance
    {
        /// <summary>
        /// Orthogonal distance from <paramref name="point"/> to the affine subspace spanned by
        /// the rows of <paramref name="S"/>, together with the nearest point on that subspace.
        /// </summary>
        /// <param name="point">Query point in R^d.</param>
        /// <param name="S">Matrix whose rows are the vertices of the hyperplane (at least 1 row).</param>
        /// <returns>
        /// Distance to the affine hull and the orthogonal projection onto it.
        /// </returns>
        /// <remarks>
        /// The affine hull is translated so that <c>S[0]</c> is the origin, then the projection
        /// matrix P = A(AᵀA)⁻¹Aᵀ is applied. Only pivot columns (linearly independent directions)
        /// are kept via reduced row echelon form to handle degenerate/coplanar vertex sets.
        /// </remarks>
        public static (double distance, Vector<double> projection) DistanceToHyperPlane(Vector<double> point, Matrix<double> S)
        {
            var n = S.RowCount;
            Vector<double> projection = null;

            // Degenerate case: single vertex — distance is just the Euclidean norm.
            if (n == 1)
                return ((point - S.Row(0)).L2Norm(), S.Row(0));

            // Translate so that S[0] is the origin; build the edge matrix A.
            var translatedS = Matrix<double>.Build.DenseOfRowVectors(S.EnumerateRows().Select(x => x - S.Row(0)).ToArray());
            var b = Matrix<double>.Build.DenseOfRowVectors(point - S.Row(0)).Transpose();
            var A = Matrix<double>.Build.DenseOfMatrix(translatedS.SubMatrix(1, translatedS.RowCount - 1, 0, translatedS.ColumnCount)).Transpose();
            var top = A.Transpose() * A;
            var bottom = A.Transpose() * b;
            var alpha = Matrix<double>.Build.DenseOfColumnVectors(top.Solve(bottom.Column(0)));

            // Drop linearly dependent columns to make AᵀA invertible.
            var (_, pivotCols) = Frref(A);
            try
            {
                A = Matrix<double>.Build.DenseOfColumnVectors(A.EnumerateColumnsIndexed().Where(x => pivotCols.Contains(x.Item1)).Select(x => x.Item2));
            }
            catch { }

            var P = A * (A.Transpose() * A).Inverse() * A.Transpose();
            var pprime = P * b;
            var distance = (pprime - b).L2Norm();
            projection = (pprime + S.SubMatrix(0, 1, 0, S.ColumnCount).Transpose()).Transpose().Row(0);
            return (distance, projection);
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="point"/> lies in the half-space defined by the
        /// affine hull of <paramref name="S"/> that contains the convex combination of its rows
        /// (i.e. barycentric coordinates are all non-negative and sum ≤ 1).
        /// </summary>
        /// <param name="point">Query point.</param>
        /// <param name="S">Simplex vertices as rows.</param>
        public static bool IsInHalfSpace(Vector<double> point, Matrix<double> S)
        {
            var n = S.RowCount;
            if (n == 1) return true;

            var translatedS = Matrix<double>.Build.DenseOfRowVectors(S.EnumerateRows().Select(x => x - S.Row(0)).ToArray());
            var b = Matrix<double>.Build.DenseOfRowVectors(point - S.Row(0)).Transpose();
            var A = Matrix<double>.Build.DenseOfMatrix(translatedS.SubMatrix(1, translatedS.RowCount - 1, 0, translatedS.ColumnCount)).Transpose();
            var top = A.Transpose() * A;
            var bottom = A.Transpose() * b;
            var alpha = Matrix<double>.Build.DenseOfColumnVectors(top.Solve(bottom.Column(0)));

            if (alpha.Exists(x => x < 0)) return true;
            if (alpha.Enumerate().Sum() <= 1) return true;
            return false;
        }

        /// <summary>
        /// Distance from <paramref name="point"/> to the nearest point inside or on the boundary of
        /// the simplex defined by the rows of <paramref name="S"/>, together with that nearest point.
        /// </summary>
        /// <param name="point">Query point in R^d.</param>
        /// <param name="S">Simplex vertices as rows (d+1 rows for a full d-simplex, fewer for faces).</param>
        /// <returns>Distance and the closest point on the simplex.</returns>
        /// <remarks>
        /// Uses a recursive GJK-style reduction:
        /// <list type="bullet">
        ///   <item>If all barycentric coordinates are ≥ 0 and sum ≤ 1, the foot is inside — return it directly.</item>
        ///   <item>If some coordinates are negative, drop those vertices and recurse on the reduced face.</item>
        ///   <item>If the sum exceeds 1, the foot is beyond the far face — drop the first vertex and recurse.</item>
        /// </list>
        /// </remarks>
        public static (double distance, Vector<double> projection) DistanceToSimplex(Vector<double> point, Matrix<double> S)
        {
            var n = S.RowCount;
            if (n == 1)
                return ((point - S.Row(0)).L2Norm(), S.Row(0));

            var translatedS = Matrix<double>.Build.DenseOfRowVectors(S.EnumerateRows().Select(x => x - S.Row(0)).ToArray());
            var b = Matrix<double>.Build.DenseOfRowVectors(point - S.Row(0)).Transpose();
            var A = Matrix<double>.Build.DenseOfMatrix(translatedS.SubMatrix(1, translatedS.RowCount - 1, 0, translatedS.ColumnCount)).Transpose();
            var top = A.Transpose() * A;
            var bottom = A.Transpose() * b;
            var alpha = Matrix<double>.Build.DenseOfColumnVectors(top.Solve(bottom.Column(0)));
            var (_, pivotCols) = Frref(A);
            A = Matrix<double>.Build.DenseOfColumnVectors(A.EnumerateColumnsIndexed().Where(x => pivotCols.Contains(x.Item1)).Select(x => x.Item2));
            var P = A * (A.Transpose() * A).Inverse() * A.Transpose();
            var pprime = P * b;

            bool allPositive = !alpha.Exists(x => x < 0);
            if (allPositive && alpha.Enumerate().Sum() <= 1)
            {
                // Point projects inside the simplex — return the orthogonal foot.
                var distance = (pprime - b).L2Norm();
                var projection = (pprime + S.SubMatrix(0, 1, 0, S.ColumnCount).Transpose()).Transpose().Row(0);
                return (distance, projection);
            }
            else if (!allPositive)
            {
                // Keep only vertices with non-negative barycentric coordinates (GJK-style face reduction).
                var sPrime = new List<Vector<double>> { S.Row(0) };
                for (int ii = 0; ii < alpha.RowCount; ii++)
                {
                    if (alpha[ii, 0] >= 0)
                        sPrime.Add(S.Row(ii + 1));
                }
                if (sPrime.Count == 1)
                    return ((point - sPrime[0]).L2Norm(), sPrime[0]);
                return DistanceToSimplex(point, Matrix<double>.Build.DenseOfRowVectors(sPrime.ToArray()));
            }
            else
            {
                // sum > 1: point is beyond the face opposite v0; recurse dropping v0.
                return DistanceToSimplex(point, Matrix<double>.Build.DenseOfMatrix(S.SubMatrix(1, S.RowCount - 1, 0, S.ColumnCount)));
            }
        }

        /// <summary>
        /// Reduced row echelon form of <paramref name="A"/> with partial pivoting.
        /// Returns the transformed matrix and the list of pivot column indices,
        /// used to identify a linearly independent basis for the column space.
        /// </summary>
        /// <param name="A">Input matrix (modified in-place on a copy).</param>
        /// <param name="tol">Threshold below which a pivot is treated as zero.</param>
        static (Matrix<double> rref, List<int> pivotCols) Frref(Matrix<double> A, double tol = 1e-6)
        {
            var m = A.RowCount;
            var n = A.ColumnCount;
            var i = 0;
            var j = 0;
            List<int> pivotCols = new();
            var W = Matrix<double>.Build.DenseOfMatrix(A);
            while (i < m && j < n)
            {
                // Find the largest absolute value in column j from row i downward.
                var abscol = W.SubMatrix(i, m - i, j, 1).Map(x => Math.Abs(x));
                var pktemp = abscol.EnumerateRows().Select((x, ind) => (x, ind)).Aggregate((a, x) => x.x.Max() > a.x.Max() ? x : a);
                var (p, k) = (pktemp.x.Max(), pktemp.ind + i);

                if (p <= tol)
                {
                    // Column is numerically zero from this row down — skip it.
                    W.ClearSubMatrix(i, m - i, j, 1);
                    j++;
                }
                else
                {
                    pivotCols.Add(j);
                    // Swap rows i and k, then eliminate all other rows in this column.
                    var rowI = Matrix<double>.Build.DenseOfMatrix(W.SubMatrix(i, 1, j, n - j));
                    var rowK = Matrix<double>.Build.DenseOfMatrix(W.SubMatrix(k, 1, j, n - j));
                    W.SetSubMatrix(i, 1, j, n - j, rowK);
                    W.SetSubMatrix(k, 1, j, n - j, rowI);
                    var pivot = W[i, j];
                    var Ai = W.SubMatrix(i, 1, j, n - j).Map(x => x / pivot);
                    W.SetSubMatrix(0, m, j, n - j, W.SubMatrix(0, m, j, n - j) - W.SubMatrix(0, m, j, 1) * Ai);
                    W.SetSubMatrix(i, 1, j, n - j, Ai);
                    i++; j++;
                }
            }
            return (W, pivotCols);
        }
    }
}
