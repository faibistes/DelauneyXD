using Delauney.Learner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delauney.Pruebas.DistanceSolver_v1;

public static class CellExtensions
{
    public static Vertex Closest(this Cell s)
    {
        Vertex dote(Vertex a, double b) => new Vertex(a.Select(x => b * x).ToArray());
        Vertex sum(IEnumerable<Vertex> e)
        {
            var ret = new Vertex(new double[e.First().Count()]);
            foreach (var v in e)
            {
                var i = 0;
                foreach (var x in v)
                {
                    ret.Position[i] += x;
                    i++;
                }
            }
            return ret;
        }
        double dj(List<Vertex> s, int j)
        {
            double dot(Vertex a, Vertex b) => a.Zip(b, (x, y) => x * y).Sum();
            Vertex minus(Vertex a, Vertex b) => new Vertex(a.Zip(b, (x, y) => x - y).ToArray());
            if (s.Count() == 0 || (s.Count() == 1 && j < 0))
            {
                throw new IndexOutOfRangeException();
            }
            if (s.Count() == 1)
            {
                return 1;
            }
            var ts = new List<Vertex>(s);
            var yj = s[j];
            ts.RemoveAt(j);
            return ts.Select((x, i) => (x, i)).Sum(x => dj(ts, x.i) * dot(minus(ts[0], yj), x.x));
        }
        var dx = s.Vertices.Select((x, i) => (x, i)).Sum(x => dj(s.Vertices.ToList(), x.i));
        return sum(s.Vertices.Select((x, i) => (x, i)).Select(x => dote(x.x, dj(s.Vertices.ToList(), x.i) / dx)));
    }

}
