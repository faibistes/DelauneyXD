using Delauney.Triangulation;
using Delauney.Triangulation.Core;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delauney.Learner;

/// <summary>
/// A labelled point in R^d. Implements <see cref="IVertex"/> for use with the triangulator
/// and <see cref="IEnumerable{T}"/> for convenient coordinate iteration.
/// </summary>
public class Vertex : IVertex, IEnumerable<double>
{
    /// <summary>Class label (e.g. species name, category). May be <c>null</c> for unlabelled points.</summary>
    public string Label { get; set; }

    /// <summary>Creates a vertex at the given coordinates with no label.</summary>
    /// <param name="vertices">Coordinate values in dimension order.</param>
    public Vertex(params double[] vertices) => Position = vertices;

    /// <summary>Creates a labelled vertex at the given coordinates.</summary>
    /// <param name="label">Class label.</param>
    /// <param name="vertices">Coordinate values in dimension order.</param>
    public Vertex(string label, params double[] vertices) : this(vertices) => Label = label;

    /// <summary>Parameterless constructor required by the triangulation framework.</summary>
    public Vertex() { }

    private Vector<double> _position;

    /// <inheritdoc/>
    public Vector<double> AsVector() => _position;

    /// <summary>Coordinate array view (alias for <see cref="Position"/>).</summary>
    public double[] AsArray() => Position;

    /// <inheritdoc/>
    public double[] Position
    {
        get => _position.AsArray();
        set => _position = Vector<double>.Build.DenseOfArray(value);
    }

    /// <inheritdoc/>
    public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)Position).GetEnumerator();

    /// <summary>Returns coordinates formatted as <c>(x,y,...)</c> using invariant culture.</summary>
    public override string ToString() =>
        $"({string.Join(",", Position.Select(x => x.ToString(CultureInfo.InvariantCulture)))})";

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}
