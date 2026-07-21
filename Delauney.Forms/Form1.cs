using Delauney.Learner;
using Delauney.Triangulation;
using Delauney.Triangulation.Core;
using MathNet.Numerics.LinearAlgebra;
using ScottPlot;

namespace Delauney.Forms
{
    /// <summary>
    /// Step-by-step 2D triangulation visualiser. Subscribes to <see cref="DelauneyTriangulator{TVertex,TFace}.RaiseTriangulationEvent"/>
    /// and redraws the ScottPlot canvas after each algorithmic step so the user can watch
    /// the ball-pivoting algorithm in action: split planes, rolling circumcircles, and
    /// the simplices being stitched together one by one.
    /// </summary>
    public partial class Form1 : Form
    {
        // Hard-coded 2D demo point set (outline of a figure-8 / two loops).
        double[][] s = new double[][]
        {
            new double[]{10,72},
            new double[]{53,76},
            new double[]{56,66},
            new double[]{63,58},
            new double[]{71,51},
            new double[]{81,48},
            new double[]{91,46},
            new double[]{101,45},
            new double[]{111,46},
            new double[]{121,47},
            new double[]{131,50},
            new double[]{140,55},
            new double[]{145,64},
            new double[]{144,74},
            new double[]{135,80},
            new double[]{125,83},
            new double[]{115,85},
            new double[]{105,87},
            new double[]{95,89},
            new double[]{85,91},
            new double[]{75,93},
            new double[]{65,95},
            new double[]{55,98},
            new double[]{45,102},
            new double[]{37,107},
            new double[]{29,114},
            new double[]{22,122},
            new double[]{19,132},
            new double[]{18,142},
            new double[]{21,151},
            new double[]{27,160},
            new double[]{35,167},
            new double[]{44,172},
            new double[]{54,175},
            new double[]{64,178},
            new double[]{74,180},
            new double[]{84,181},
            new double[]{94,181},
            new double[]{104,181},
            new double[]{114,181},
            new double[]{124,181},
            new double[]{134,179},
            new double[]{144,177},
            new double[]{153,173},
            new double[]{162,168},
            new double[]{171,162},
            new double[]{177,154},
            new double[]{182,145},
            new double[]{184,135},
            new double[]{139,132},
            new double[]{136,142},
            new double[]{128,149},
            new double[]{119,153},
            new double[]{109,155},
            new double[]{99,155},
            new double[]{89,155},
            new double[]{79,153},
            new double[]{69,150},
            new double[]{61,144},
            new double[]{63,134},
            new double[]{72,128},
            new double[]{82,125},
            new double[]{92,123},
            new double[]{102,121},
            new double[]{112,119},
            new double[]{122,118},
            new double[]{132,116},
            new double[]{142,113},
            new double[]{151,110},
            new double[]{161,106},
            new double[]{170,102},
            new double[]{178,96},
            new double[]{185,88},
            new double[]{189,78},
            new double[]{190,68},
            new double[]{189,58},
            new double[]{185,49},
            new double[]{179,41},
            new double[]{171,34},
            new double[]{162,29},
            new double[]{153,25},
            new double[]{143,23},
            new double[]{133,21},
            new double[]{123,19},
            new double[]{113,19},
            new double[]{102,19},
            new double[]{92,19},
            new double[]{82,19},
            new double[]{72,21},
            new double[]{62,22},
            new double[]{52,25},
            new double[]{43,29},
            new double[]{33,34},
            new double[]{25,41},
            new double[]{19,49},
            new double[]{14,58},
            new double[]{21,73},
            new double[]{31,74},
            new double[]{42,74},
            new double[]{173,134},
            new double[]{161,134},
            new double[]{150,133},
            new double[]{97,104},
            new double[]{52,117},
            new double[]{157,156},
            new double[]{94,171},
            new double[]{112,106},
            new double[]{169,73},
            new double[]{58,165},
            new double[]{149,40},
            new double[]{70,33},
            new double[]{147,157},
            new double[]{48,153},
            new double[]{140,96},
            new double[]{47,129},
            new double[]{173,55},
            new double[]{144,86},
            new double[]{159,67},
            new double[]{150,146},
            new double[]{38,136},
            new double[]{111,170},
            new double[]{124,94},
            new double[]{26,59},
            new double[]{60,41},
            new double[]{71,162},
            new double[]{41,64},
            new double[]{88,110},
            new double[]{122,34},
            new double[]{151,97},
            new double[]{157,56},
            new double[]{39,146},
            new double[]{88,33},
            new double[]{159,45},
            new double[]{47,56},
            new double[]{138,40},
            new double[]{129,165},
            new double[]{33,48},
            new double[]{106,31},
            new double[]{169,147},
            new double[]{37,122},
            new double[]{71,109},
            new double[]{163,89},
            new double[]{37,156},
            new double[]{82,170},
            new double[]{180,72},
            new double[]{29,142},
            new double[]{46,41},
            new double[]{59,155},
            new double[]{124,106},
            new double[]{157,80},
            new double[]{175,82},
            new double[]{56,50},
            new double[]{62,116},
            new double[]{113,95},
            new double[]{144,167}
        };

        private void printPreviewDialog1_Load(object sender, EventArgs e) { }

        public Form1() { InitializeComponent(); }

        private void Form1_Load(object sender, EventArgs e) { }

        /// <summary>
        /// Starts a triangulation with events enabled and redraws the plot after each step.
        /// Runs on a background thread so the UI stays responsive; the event handler
        /// marshals back to the UI thread via <c>Invoke</c>.
        /// </summary>
        private async void button1_Click(object sender, EventArgs e)
        {
            Actions.Clear();
            button1.Enabled = false;
            var t  = new DelauneyTriangulator<Vertex, Cell>(withEvents: true);
            var ls = s.Select(x => new Vertex("s", x)).ToList();
            t.RaiseTriangulationEvent += T_RaiseTriangulationEvent;
            await Task.Run(() => t.CreateDelaunay(ls));
            formsPlot2.RefreshRequest(RenderType.LowQualityThenHighQualityDelayed);
            this.Refresh();
            button1.Enabled = true;
        }

        /// <summary>
        /// Redraws all recorded events onto the ScottPlot canvas:
        /// points, split planes (green), circumcircles (black), candidate markers (red),
        /// transient triangles (red), and final triangles (black).
        /// </summary>
        private void UpdatePlot()
        {
            formsPlot2.Plot.Clear();

            // Draw all input points with index labels.
            foreach (var x in Enumerable.Range(0, s.Length - 1))
            {
                var m = formsPlot2.Plot.AddMarker(s[x][0], s[x][1], MarkerShape.filledCircle, 3, Color.Black);
                m.Text = x.ToString();
                m.TextFont.Size = 9;
            }

            foreach (var a in Actions)
            {
                switch (a.Type)
                {
                    case TriangulationEventType.Face:
                        formsPlot2.Plot.AddLine(a.Face[0][0], a.Face[0][1], a.Face[1][0], a.Face[1][1], Color.Black, 4);
                        break;

                    case TriangulationEventType.Ball:
                        formsPlot2.Plot.AddCircle(a.Center[0], a.Center[1], a.Radius, Color.Black);
                        formsPlot2.Plot.AddMarker(a.Center[0], a.Center[1], MarkerShape.cross, 2, Color.Black);
                        break;

                    case TriangulationEventType.FirstTriangle:
                        formsPlot2.Plot.AddPolygon(
                            a.Triangle.Select(x => x[0]).ToArray(),
                            a.Triangle.Select(x => x[1]).ToArray(),
                            null, 1, Color.Red);
                        break;

                    case TriangulationEventType.Triangle:
                        formsPlot2.Plot.AddPolygon(
                            a.Triangle.Select(x => x[0]).ToArray(),
                            a.Triangle.Select(x => x[1]).ToArray(),
                            Color.Transparent, 1,
                            a.Transient ? Color.Red : Color.Black);
                        break;

                    case TriangulationEventType.LocalTriangle:
                        // Currently not rendered — placeholder for future local-candidate visualisation.
                        break;

                    case TriangulationEventType.Line:
                        // Currently not rendered.
                        break;

                    case TriangulationEventType.Plane:
                        formsPlot2.Plot.AddLine(a.Plane[0][0], a.Plane[0][1], a.Plane[1][0], a.Plane[1][1], Color.Green, 2);
                        break;

                    case TriangulationEventType.Candidates:
                        foreach (var x in Enumerable.Range(0, a.Candidates.Length - 1))
                        {
                            var m = formsPlot2.Plot.AddMarker(
                                a.Candidates[x][0], a.Candidates[x][1],
                                MarkerShape.filledCircle, 3, Color.Red);
                            m.Text = x.ToString();
                            m.TextFont.Size = 7;
                        }
                        break;
                }
            }

            formsPlot2.RefreshRequest(RenderType.LowQualityThenHighQualityDelayed);
            this.Refresh();
        }

        // All events received so far, kept in insertion order for replay-style rendering.
        private List<TriangulationEventArgs> Actions = new();

        // Ensures UpdatePlot is not called concurrently (the triangulator fires events fast).
        SemaphoreSlim m = new(1);

        /// <summary>
        /// Handles a single triangulation step event. Marshals to the UI thread if needed,
        /// prunes transient events of the same type (so rolling balls replace each other),
        /// appends the new event, and redraws.
        /// </summary>
        private void T_RaiseTriangulationEvent(object? sender, TriangulationEventArgs e)
        {
            if (InvokeRequired) { Invoke(() => T_RaiseTriangulationEvent(sender, e)); return; }
            m.Wait();
            if (e.Transient || e.ErasePrevious)
                Actions.RemoveAll(x => x.Type == e.Type && ((x.Transient && e.Transient) || e.ErasePrevious));
            Actions.Add(e);
            UpdatePlot();
            m.Release();
        }
    }
}
