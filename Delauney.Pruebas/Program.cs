// See https://aka.ms/new-console-template for more information
using static MathNet.Numerics.Statistics.Statistics;
using System;
using System.Collections;
using Delauney.Util;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Serialization;
using System.Windows.Markup;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra;
using System.Runtime.InteropServices;
using Microsoft.ML;
using Delauney.Learner;
using CsvHelper;
using static System.Net.Mime.MediaTypeNames;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using System.Drawing;
using Delauney.Distances;
using MathNet.Numerics.Providers.LinearAlgebra;
using MathNet.Numerics.Distributions;
using Apache.Arrow.Types;
using static Apache.Arrow.Memory.MemoryAllocator;
using KdTree;
using KdTree.Math;
using System.Runtime.CompilerServices;
using Delauney.Triangulation;
using Delauney.Triangulation.Core;

MathNet.Numerics.Control.UseBestProviders();
await PerfTest();
//await Compare();
//await Ese();
static async Task PerfTest()
{
    // Simplex count is O(N^ceil(d/2)), so sizes must shrink fast with dimension.
    var plan = new (int d, int[] sizes)[]
    {
        (2, new[] { 100, 300, 600, 1000 }),
        (3, new[] {  30,  60,  100 }),
        (4, new[] {  10,  20,   30 }),
    };

    var rng = new Random(42);
    double Gauss() { double u = 1 - rng.NextDouble(), v = 1 - rng.NextDouble();
                     return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

    Console.WriteLine($"{"D",2}  {"N",5}  {"local(ms)",10}  {"brute(ms)",10}  {"speedup",8}  {"simplices",10}");
    Console.WriteLine(new string('-', 58));

    foreach (var (d, sizes) in plan)
    foreach (var n in sizes)
    {
        var pts = Enumerable.Range(0, n)
            .Select(_ => new Vertex("x", Enumerable.Range(0, d).Select(_ => Gauss()).ToArray()))
            .ToList();

        long msLocal, msBrute;
        int simplices;

        {
            var sw = Stopwatch.StartNew();
            var tri = new DelauneyTriangulator<Vertex, Cell>() { UseLocalCandidates = true }
                          .CreateDelaunay(pts);
            sw.Stop();
            msLocal   = sw.ElapsedMilliseconds;
            simplices = tri.Cells.Count();
        }
        {
            var sw = Stopwatch.StartNew();
            new DelauneyTriangulator<Vertex, Cell>() { UseLocalCandidates = false }
                .CreateDelaunay(pts);
            sw.Stop();
            msBrute = sw.ElapsedMilliseconds;
        }

        var speedup = msBrute > 0 ? (double)msBrute / msLocal : double.NaN;
        Console.WriteLine($"{d,2}  {n,5}  {msLocal,10}  {msBrute,10}  {speedup,8:F2}x  {simplices,10}");
    }
}
static async Task Ese()
{
    double[][] s = new double[][]
{new double[]{10,72},
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
 new double[]{144,167}};
    
    /*double[][] s = new double[][]
    {
        new double[]{1,1 },
        new double[]{4,1},
        new double[]{5,1 },
        new double[]{3,6 },
        new double[]{2,0.2 },
        new double[]{2,-2 },
        new double[]{3,8 },
    };*/
    var ls = s.Select(x => new Vertex("s", x)).ToList();
    /*var t = new DelauneyTriangulator<Vertex, Cell>(true);
    Stopwatch sw = new();
    sw.Start();
    var tt = t.CreateDelaunay(ls);
    sw.Stop();
    Console.WriteLine(sw.ElapsedMilliseconds.ToString("###,###,###,###"));*/
    
}
static async Task Iris()
{
    var reader = new StreamReader(@".\iris.data");
    var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
    var csv = new CsvReader(reader, config);
    var records = csv.GetRecords<IrisRecord>().ToList();
    var d = new Dictionary<string, IList<Vertex>>();
    var tt = records.Split(.2, 20);
    var stats = records.Select(x => x.AsArray()).ToArray();
    var means = new double[stats.First().Length];
    var devs = new double[stats.First().Length];
    foreach (var i in Enumerable.Range(0, stats.First().Length))
    {
        means[i] = stats.Select(x => x[i]).GeometricMean();
        devs[i] = stats.Select(x => x[i]).StandardDeviation();
    }
    foreach (var x in tt.Train)
    {
        var v = new Vertex(x.Label, x.AsZNormArray(means, devs));
        //var v = new Vertex(x.Label, x.SepalLength, x.SepalWidth, x.PetalLength, x.PetalWidth);
        if (!d.ContainsKey(x.Label))
        {
            d.Add(x.Label, new List<Vertex>());
        }
        d[x.Label].Add(v);
    }
    Stopwatch sw = new();
    sw.Start();
    var l = new Learner(d);
    await l.BuildTriangulations();
    await l.MakeIndex(0.9);
    var test= tt.Test.Select(x => x.AsZNormArray(means, devs)).ToArray();
    //var test = tt.Test.Select(x => x.AsArray()).ToArray();
    var pred = await l.Predict(test, 20);
    sw.Stop();
    double acc = 0; double prec = 0; double recall = 0;
    double tacc = 0; double tprec = 0; double trecall = 0;
    foreach (var k in d.Keys)
    {
        double tp = tt.Test.Zip(pred).Count(x => x.First.Label == x.Second && k == x.First.Label);
        double fn = tt.Test.Zip(pred).Count(x => x.First.Label != x.Second && k == x.First.Label);
        double tn = tt.Test.Zip(pred).Count(x => x.Second != k && k != x.First.Label);
        double fp = tt.Test.Zip(pred).Count(x => x.First.Label != k && x.Second == k);
        double total = tt.Test.Count(x => x.Label == k);
        acc = tp / total;
        tacc += acc;
        prec = tp / (tp + fp);
        tprec += prec;
        recall = tp / (tp + fn);
        trecall += recall;
        Console.WriteLine($"{k} acc =>{acc}");
        Console.WriteLine($"{k} prec =>{prec}");
        Console.WriteLine($"{k} rec =>{recall}");
    }
    Console.WriteLine($"TOTAL acc =>{tacc/d.Count}");
    Console.WriteLine($"TOTAL prec =>{tprec / d.Count}");
    Console.WriteLine($"TOTAL rec =>{trecall/d.Count}");
    Console.WriteLine("Tiempo (train+predict): "+sw.ElapsedMilliseconds.ToString("###,###,###")+ "ms.");
}

static async Task CancerKNN()
{
    var reader = new StreamReader(@".\wdbc.data");
    var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
    var csv = new CsvReader(reader, config);
    var records = csv.GetRecords<CancerRecord>().ToList();
    var tt = records.Split(.2, 20);
    var stats = records.Select(x => x.AsArray()).ToArray();
    var means = new double[stats.First().Length];
    var devs = new double[stats.First().Length];
    var tree = new KdTree<double,CancerRecord>(31,new DoubleMath());
    foreach (var i in Enumerable.Range(0, stats.First().Length))
    {
        means[i] = stats.Select(x => x[i]).GeometricMean();
        devs[i] = stats.Select(x => x[i]).StandardDeviation();
    }
    foreach (var x in tt.Train)
    {
        tree.Add(x.AsZNormArray(means,devs), x);
        //var v = new Vertex(x.Label, x.SepalLength, x.SepalWidth, x.PetalLength, x.PetalWidth);
    }
    Stopwatch sw = new();
    sw.Start();
    var test = tt.Test.Select(x => x.AsZNormArray(means, devs)).ToArray();
    var pred = test.Select(x => new
    {
        Test = x,
        Label = tree.GetNearestNeighbours(x, 20)
                                                .Select(n => new
                                                {
                                                    Label = n.Value.Diagnosis,
                                                    Distance = (Vector<double>.Build.DenseOfArray(n.Point)
                                                                             - Vector<double>.Build.DenseOfArray(x)).L2Norm()
                                                })
                                                .GroupBy(n => n.Label).Select(y => new
                                                {
                                                    Label = y.Key,
                                                    Average = y.Average(z => z.Distance)
                                                })
                                                .OrderBy(o => o.Average)
                                                .Select(s => s.Label)
                                                .First()
    });
    sw.Stop();
    double acc = 0; double prec = 0; double recall = 0;
    double tacc = 0; double tprec = 0; double trecall = 0;
    List<string> d = tt.Train.GroupBy(x => x.Diagnosis).Select(x=>x.Key).ToList();
    foreach (var k in d)
    {
        double tp = tt.Test.Zip(pred).Count(x => x.First.Diagnosis == x.Second.Label && k == x.First.Diagnosis);
        double fn = tt.Test.Zip(pred).Count(x => x.First.Diagnosis != x.Second.Label && k == x.First.Diagnosis);
        double tn = tt.Test.Zip(pred).Count(x => x.Second.Label != k && k != x.First.Diagnosis);
        double fp = tt.Test.Zip(pred).Count(x => x.First.Diagnosis != k && x.Second.Label == k);
        double total = tt.Test.Count(x => x.Diagnosis == k);
        acc = tp / total;
        tacc += acc;
        prec = tp / (tp + fp);
        tprec += prec;
        recall = tp / (tp + fn);
        trecall += recall;
        Console.WriteLine($"{k} acc =>{acc}");
        Console.WriteLine($"{k} prec =>{prec}");
        Console.WriteLine($"{k} rec =>{recall}");
    }
    Console.WriteLine($"TOTAL acc =>{tacc / d.Count}");
    Console.WriteLine($"TOTAL prec =>{tprec / d.Count}");
    Console.WriteLine($"TOTAL rec =>{trecall / d.Count}");
    Console.WriteLine("Tiempo (train+predict): " + sw.ElapsedMilliseconds.ToString("###,###,###") + "ms.");
}
static async Task IrisKNN()
{
    var reader = new StreamReader(@".\iris.data");
    var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
    var csv = new CsvReader(reader, config);
    var records = csv.GetRecords<IrisRecord>().ToList();
    var tt = records.Split(.2, 20);
    var stats = records.Select(x => x.AsArray()).ToArray();
    var means = new double[stats.First().Length];
    var devs = new double[stats.First().Length];
    var tree = new KdTree<double, IrisRecord>(4, new DoubleMath());
    foreach (var i in Enumerable.Range(0, stats.First().Length))
    {
        means[i] = stats.Select(x => x[i]).GeometricMean();
        devs[i] = stats.Select(x => x[i]).StandardDeviation();
    }
    foreach (var x in tt.Train)
    {
        tree.Add(x.AsZNormArray(means, devs), x);
        //var v = new Vertex(x.Label, x.SepalLength, x.SepalWidth, x.PetalLength, x.PetalWidth);
    }
    Stopwatch sw = new();
    sw.Start();
    var test = tt.Test.Select(x => x.AsZNormArray(means, devs)).ToArray();
    var pred = test.Select(x => new
    {
        Test = x,
        Label = tree.GetNearestNeighbours(x, 20)
                                                .Select(n => new
                                                {
                                                    Label = n.Value.Label,
                                                    Distance = (Vector<double>.Build.DenseOfArray(n.Point)
                                                                             - Vector<double>.Build.DenseOfArray(x)).L2Norm()
                                                })
                                                .GroupBy(n => n.Label).Select(y => new
                                                {
                                                    Label = y.Key,
                                                    Average = y.Average(z => z.Distance)
                                                })
                                                .OrderBy(o => o.Average)
                                                .Select(s => s.Label)
                                                .First()
    });
    sw.Stop();
    double acc = 0; double prec = 0; double recall = 0;
    double tacc = 0; double tprec = 0; double trecall = 0;
    List<string> d = tt.Train.GroupBy(x => x.Label).Select(x => x.Key).ToList();
    foreach (var k in d)
    {
        double tp = tt.Test.Zip(pred).Count(x => x.First.Label == x.Second.Label && k == x.First.Label);
        double fn = tt.Test.Zip(pred).Count(x => x.First.Label != x.Second.Label && k == x.First.Label);
        double tn = tt.Test.Zip(pred).Count(x => x.Second.Label != k && k != x.First.Label);
        double fp = tt.Test.Zip(pred).Count(x => x.First.Label != k && x.Second.Label == k);
        double total = tt.Test.Count(x => x.Label == k);
        acc = tp / total;
        tacc += acc;
        prec = tp / (tp + fp);
        tprec += prec;
        recall = tp / (tp + fn);
        trecall += recall;
        Console.WriteLine($"{k} acc =>{acc}");
        Console.WriteLine($"{k} prec =>{prec}");
        Console.WriteLine($"{k} rec =>{recall}");
    }
    Console.WriteLine($"TOTAL acc =>{tacc / d.Count}");
    Console.WriteLine($"TOTAL prec =>{tprec / d.Count}");
    Console.WriteLine($"TOTAL rec =>{trecall / d.Count}");
    Console.WriteLine("Tiempo (train+predict): " + sw.ElapsedMilliseconds.ToString("###,###,###") + "ms.");
}

static async Task Cancer()
{
    var reader = new StreamReader(@".\wdbc.data");
    var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
    var csv = new CsvReader(reader, config);
    var records = csv.GetRecords<CancerRecord>().ToList();
    var stats = records.Select(x => x.AsArray()).ToArray();
    var means = new double[stats.First().Length];
    var devs = new double[stats.First().Length];
    foreach (var i in Enumerable.Range(0, stats.First().Length))
    {
        means[i] = stats.Select(x => x[i]).GeometricMean();
        devs[i] = stats.Select(x => x[i]).StandardDeviation();
    }
    var d = new Dictionary<string, IList<Vertex>>();
    var tt = records.Split(.2, 20);
    foreach (var x in tt.Train)
    {
        var v = new Vertex(x.Diagnosis, x.AsZNormArray(means, devs));
        if (!d.ContainsKey(x.Diagnosis))
        {
            d.Add(x.Diagnosis, new List<Vertex>());
        }
        d[x.Diagnosis].Add(v);
    }
    Stopwatch sw = new();
    sw.Start();
    var l = new Learner(d);
    await l.BuildTriangulations();
    await l.MakeIndex(0.9);
    var test = tt.Test.Select(x => x.AsZNormArray(means, devs)).ToArray();
    var pred = await l.Predict(test, 20);
    sw.Stop();
    double acc = 0; double prec = 0; double recall = 0;
    double tacc = 0; double tprec = 0; double trecall = 0;
    foreach (var k in d.Keys)
    {
        double tp = tt.Test.Zip(pred).Count(x => x.First.Diagnosis == x.Second && k == x.First.Diagnosis);
        double fn = tt.Test.Zip(pred).Count(x => x.First.Diagnosis != x.Second && k == x.First.Diagnosis);
        double tn = tt.Test.Zip(pred).Count(x => x.Second != k && k != x.First.Diagnosis);
        double fp = tt.Test.Zip(pred).Count(x => x.First.Diagnosis != k && x.Second == k);
        double total = tt.Test.Count(x => x.Diagnosis == k);
        acc = tp / total;
        tacc += acc;
        prec = tp / (tp + fp);
        tprec += prec;
        recall = tp / (tp + fn);
        trecall += recall;
        Console.WriteLine($"{k} acc =>{acc}");
        Console.WriteLine($"{k} prec =>{prec}");
        Console.WriteLine($"{k} rec =>{recall}");
    }
    Console.WriteLine($"TOTAL acc =>{tacc / d.Count}");
    Console.WriteLine($"TOTAL prec =>{tprec / d.Count}");
    Console.WriteLine($"TOTAL rec =>{trecall / d.Count}");
    Console.WriteLine(sw.ElapsedMilliseconds.ToString("###,###,###"));

}
static void PrintMetricsInline(List<string> trueLabels, string[] pred, IEnumerable<string> keys)
{
    double tacc = 0, tprec = 0, trec = 0; int n = 0;
    foreach (var k in keys)
    {
        double tp = trueLabels.Zip(pred).Count(x => x.First == x.Second && x.First == k);
        double fn = trueLabels.Zip(pred).Count(x => x.First != x.Second && x.First == k);
        double fp = trueLabels.Zip(pred).Count(x => x.First != k && x.Second == k);
        double total = trueLabels.Count(x => x == k);
        double prec = tp + fp > 0 ? tp / (tp + fp) : 0;
        tacc += tp / total; tprec += prec; trec += tp / (tp + fn); n++;
    }
    Console.WriteLine($"acc={tacc/n:F3}  prec={tprec/n:F3}  rec={trec/n:F3}");
}
static void PrintMetrics(string tag, List<string> trueLabels, string[] pred, IEnumerable<string> keys)
{
    Console.WriteLine($"  [{tag}]");
    double tacc = 0, tprec = 0, trec = 0;
    int n = 0;
    foreach (var k in keys)
    {
        double tp = trueLabels.Zip(pred).Count(x => x.First == x.Second && x.First == k);
        double fn = trueLabels.Zip(pred).Count(x => x.First != x.Second && x.First == k);
        double fp = trueLabels.Zip(pred).Count(x => x.First != k && x.Second == k);
        double total = trueLabels.Count(x => x == k);
        double prec = tp + fp > 0 ? tp / (tp + fp) : 0;
        tacc += tp / total; tprec += prec; trec += tp / (tp + fn); n++;
    }
    Console.WriteLine($"    acc={tacc/n:F3}  prec={tprec/n:F3}  rec={trec/n:F3}");
}

static string KnnPredict(double[] point, KdTree<double, string> tree, int k)
{
    return tree.GetNearestNeighbours(point, k)
               .GroupBy(n => n.Value)
               .OrderByDescending(g => g.Count())
               .First().Key;
}

static async Task Compare()
{
    // --- Iris ---
    Console.WriteLine("=== Iris ===");
    {
        var reader = new StreamReader(@".\iris.data");
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        var records = new CsvReader(reader, config).GetRecords<IrisRecord>().ToList();
        var tt = records.Split(.2, 20);
        var stats = records.Select(x => x.AsArray()).ToArray();
        var means = Enumerable.Range(0, 4).Select(i => stats.Select(x => x[i]).GeometricMean()).ToArray();
        var devs  = Enumerable.Range(0, 4).Select(i => stats.Select(x => x[i]).StandardDeviation()).ToArray();

        var trainData = new Dictionary<string, IList<Vertex>>();
        var knnTree = new KdTree<double, string>(4, new DoubleMath());
        foreach (var x in tt.Train)
        {
            var p = x.AsZNormArray(means, devs);
            if (!trainData.ContainsKey(x.Label)) trainData[x.Label] = new List<Vertex>();
            trainData[x.Label].Add(new Vertex(x.Label, p));
            knnTree.Add(p, x.Label);
        }
        var testPoints = tt.Test.Select(x => x.AsZNormArray(means, devs)).ToArray();
        var testLabels = tt.Test.Select(x => x.Label).ToList();

        var l = new Learner(trainData);
        await l.BuildTriangulations();
        await l.MakeIndex(2.0);
        var delPred = await l.Predict(testPoints, 10);
        var knnPred = testPoints.Select(p => KnnPredict(p, knnTree, 5)).ToArray();

        PrintMetrics("Delaunay", testLabels, delPred, trainData.Keys);
        PrintMetrics("KNN k=5 ", testLabels, knnPred, trainData.Keys);
    }

    // --- Peterson & Barney vowel formants (F1/F2 only, 2D) ---
    Console.WriteLine("=== Peterson & Barney vowels (F1/F2) ===");
    {
        var reader = new StreamReader(@".\pb.csv");
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        var records = new CsvReader(reader, config).GetRecords<PbRecord>().ToList();

        // z-normalise F1/F2
        double meanF1 = records.Average(x => x.F1), sdF1 = records.Select(x=>x.F1).StandardDeviation();
        double meanF2 = records.Average(x => x.F2), sdF2 = records.Select(x=>x.F2).StandardDeviation();
        double[] Norm(PbRecord r) => new[] { (r.F1-meanF1)/sdF1, (r.F2-meanF2)/sdF2 };

        // train on repetition 1, test on repetition 2 — men only (most compact vowel space)
        var train = records.Where(x => x.Repetition == 1 && x.Type == "m").ToList();
        var test  = records.Where(x => x.Repetition == 2 && x.Type == "m").ToList();

        var trainData = new Dictionary<string, IList<Vertex>>();
        var knnTree = new KdTree<double, string>(2, new DoubleMath());
        foreach (var x in train)
        {
            var p = Norm(x);
            if (!trainData.ContainsKey(x.Vowel)) trainData[x.Vowel] = new List<Vertex>();
            trainData[x.Vowel].Add(new Vertex(x.Vowel, p));
            knnTree.Add(p, x.Vowel);
        }
        var testPoints = test.Select(Norm).ToArray();
        var testLabels = test.Select(x => x.Vowel).ToList();

        // KNN baseline across k values
        foreach (var kk in new[] { 1, 3, 5, 9, 15 })
        {
            var knnPred = testPoints.Select(p => KnnPredict(p, knnTree, kk)).ToArray();
            Console.Write($"  KNN k={kk,-3}  ");
            PrintMetricsInline(testLabels, knnPred, trainData.Keys);
        }

        var l = new Learner(trainData);
        await l.BuildTriangulations();
        foreach (var factor in new[] { 1.2, 1.5, 2.0, 3.0, 5.0, 10.0, double.MaxValue })
        {
            await l.MakeIndex(factor);
            foreach (var kk in new[] { 3, 5, 10, 20 })
            {
                var delPred = await l.Predict(testPoints, kk);
                Console.Write($"  Del f={factor,8:G3} k={kk,-3}  ");
                PrintMetricsInline(testLabels, delPred, trainData.Keys);
            }
        }
    }

    // --- Synthetic sparse blobs (Delaunay's home turf) ---
    // 3 well-separated Gaussian clusters in 2D, sparse training, dense test
    // Interior test points are far from training samples — KNN may straddle boundaries
    Console.WriteLine("=== Sparse blobs 2D ===");
    {
        var rng = new Random(7);
        double[] centers = new double[] { 0, 0,   8, 0,   4, 7 };
        var labels = new[] { "A", "B", "C" };
        double Gauss() { double u = 1-rng.NextDouble(); double v = 1-rng.NextDouble();
                         return Math.Sqrt(-2*Math.Log(u))*Math.Cos(2*Math.PI*v); }

        var trainData = new Dictionary<string, IList<Vertex>>();
        var knnTree = new KdTree<double, string>(2, new DoubleMath());
        // 12 training points per class — deliberately sparse
        for (int c = 0; c < 3; c++)
        {
            trainData[labels[c]] = new List<Vertex>();
            for (int i = 0; i < 12; i++)
            {
                var p = new[] { centers[c*2] + Gauss(), centers[c*2+1] + Gauss() };
                trainData[labels[c]].Add(new Vertex(labels[c], p));
                knnTree.Add(p, labels[c]);
            }
        }
        // 40 test points per class, slightly tighter (std=0.7) to stay interior
        var testPoints = new List<double[]>();
        var testLabels = new List<string>();
        for (int c = 0; c < 3; c++)
            for (int i = 0; i < 40; i++)
            {
                testPoints.Add(new[] { centers[c*2] + 0.7*Gauss(), centers[c*2+1] + 0.7*Gauss() });
                testLabels.Add(labels[c]);
            }

        var l = new Learner(trainData);
        await l.BuildTriangulations();
        await l.MakeIndex(2.0);
        var delPred = await l.Predict(testPoints.ToArray(), 6);
        var knnPred = testPoints.Select(p => KnnPredict(p, knnTree, 5)).ToArray();

        PrintMetrics("Delaunay", testLabels, delPred, trainData.Keys);
        PrintMetrics("KNN k=5 ", testLabels, knnPred, trainData.Keys);
    }
}

static async Task Rings()
{
    var rng = new Random(42);
    double[] Polar(double r, double theta) => new[] { r * Math.Cos(theta), r * Math.Sin(theta) };

    // disk: uniform in r=0..0.8 — lives inside the hole of ring's convex hull
    var allInner = Enumerable.Range(0, 100)
        .Select(_ => Polar(0.8 * Math.Sqrt(rng.NextDouble()), rng.NextDouble() * 2 * Math.PI)).ToList();
    // ring: r=1..2 — non-convex, its convex hull is a disk of r=2 that swallows "disk"
    var allOuter = Enumerable.Range(0, 100)
        .Select(_ => Polar(1 + rng.NextDouble(), rng.NextDouble() * 2 * Math.PI)).ToList();

    var trainData = new Dictionary<string, IList<Vertex>>
    {
        ["disk"] = allInner.Take(80).Select(p => new Vertex("disk", p)).ToList<Vertex>(),
        ["ring"] = allOuter.Take(80).Select(p => new Vertex("ring", p)).ToList<Vertex>()
    };
    var testLabels = Enumerable.Repeat("disk", 20).Concat(Enumerable.Repeat("ring", 20)).ToList();
    var testPoints = allInner.Skip(80).Concat(allOuter.Skip(80)).ToArray();

    Console.WriteLine("Building triangulations...");
    var l = new Learner(trainData);
    await l.BuildTriangulations();

    foreach (var factor in new[] { 1.0, 1.2, 1.5, 2.0, 3.0, 5.0, 10.0, double.MaxValue })
    {
        await l.MakeIndex(factor);
        var pred = await l.Predict(testPoints, 10);
        Console.Write($"factor={factor,12:G4}  ");
        foreach (var k in trainData.Keys)
        {
            double tp = testLabels.Zip(pred).Count(x => x.First == x.Second && k == x.First);
            double fn = testLabels.Zip(pred).Count(x => x.First != x.Second && k == x.First);
            double fp = testLabels.Zip(pred).Count(x => x.First != k && x.Second == k);
            double total = testLabels.Count(x => x == k);
            double prec = tp + fp > 0 ? tp / (tp + fp) : 0;
            Console.Write($"[{k}] acc={tp/total:F2} prec={prec:F2} rec={tp/(tp+fn):F2}  ");
        }
        Console.WriteLine();
    }
}

class PbRecord
{
    [Name("type")]       public string Type       { get; set; }
    [Name("gender")]     public string Gender     { get; set; }
    [Name("speaker")]    public int    Speaker    { get; set; }
    [Name("vowel")]      public string Vowel      { get; set; }
    [Name("repetition")] public int    Repetition { get; set; }
    [Name("F0")]         public double F0         { get; set; }
    [Name("F1")]         public double F1         { get; set; }
    [Name("F2")]         public double F2         { get; set; }
    [Name("F3")]         public double F3         { get; set; }
}
class IrisRecord
{
    [Index(0)] public double SepalLength { get; set; }
    [Index(1)] public double SepalWidth { get; set; }
    [Index(2)] public double PetalLength { get; set; }
    [Index(3)] public double PetalWidth { get; set; }
    [Index(4)] public string Label { get; set; }
    public double[] AsArray() => new double[]
{
    SepalLength,SepalWidth,PetalLength,PetalWidth
};
    public double[] AsZNormArray(double[] means, double[] devs) =>
    AsArray().Select((x, i) => ((x - means[i]) / devs[i]) * 100).ToArray();

}
class CancerRecord
{
    [Index(0)] public int Id { get; set; }
    [Index(1)] public string Diagnosis { get; set; }
    [Index(2)] public double radius1 { get; set; }
    [Index(3)] public double texture1 { get; set; }
    [Index(4)] public double perimeter1 { get; set; }
    [Index(5)] public double area1 { get; set; }
    [Index(6)] public double smoothness1 { get; set; }
    [Index(7)] public double compactness1 { get; set; }
    [Index(8)] public double concavity1 { get; set; }
    [Index(9)] public double concave_points1 { get; set; }
    [Index(10)] public double symmetry1 { get; set; }
    [Index(11)] public double fractal_dimension1 { get; set; }
    [Index(12)] public double radius2 { get; set; }
    [Index(13)] public double texture2 { get; set; }
    [Index(14)] public double perimeter2 { get; set; }
    [Index(15)] public double area2 { get; set; }
    [Index(16)] public double smoothness2 { get; set; }
    [Index(17)] public double compactness2 { get; set; }
    [Index(18)] public double concavity2 { get; set; }
    [Index(19)] public double concave_points2 { get; set; }
    [Index(20)] public double symmetry2 { get; set; }
    [Index(21)] public double fractal_dimension2 { get; set; }
    [Index(22)] public double radius3 { get; set; }
    [Index(23)] public double texture3 { get; set; }
    [Index(24)] public double perimeter3 { get; set; }
    [Index(25)] public double area3 { get; set; }
    [Index(26)] public double smoothness3 { get; set; }
    [Index(27)] public double compactness3 { get; set; }
    [Index(28)] public double concavity3 { get; set; }
    [Index(29)] public double concave_points3 { get; set; }
    [Index(30)] public double symmetry3 { get; set; }
    [Index(31)] public double fractal_dimension3 { get; set; }
    public double[] AsZNormArray(double[] means, double[] devs) =>
        AsArray().Select((x, i) => ((x - means[i]) / devs[i]) * 100).ToArray();
    public double[] AsArray() => new double[]
    {
 radius1,
 texture1,
 perimeter1,
 area1,
 smoothness1,
 compactness1,
 concavity1,
 concave_points1,
 symmetry1,
 fractal_dimension1,
 radius2,
 texture2,
 perimeter2,
 area2,
 smoothness2,
 compactness2,
 concavity2,
 concave_points2,
 symmetry2,
 fractal_dimension2,
 radius3,
 texture3,
 perimeter3,
 area3,
 smoothness3,
 compactness3,
 concavity3,
 concave_points3,
 symmetry3,
 fractal_dimension3
    };
}
