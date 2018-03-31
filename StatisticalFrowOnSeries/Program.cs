using forms;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Console;
namespace StatisticalFrowOnSeries
{
    public class Program
    {

        static void DrawChart(List<Candle> candles)
        {
            var c = candles.Take(100);
            using (var python = Py.GIL())
            {
                dynamic py = Py.Import("plotly.plotly");
                dynamic go = Py.Import("plotly.graph_objs");
                var datetime = Py.Import("datetime");
                var trace = go.Candlestick(x: c.Select((x, i) => i).ToList(),
                       open: c.Select((x, i) => x.Open).ToList(),
                       high: c.Select((x, i) => x.High).ToList(),
                       low: c.Select((x, i) => x.Low).ToList(),
                       close: c.Select((x, i) => x.Close).ToList());
                var data = new List<dynamic>();
                data.Add(trace);
                py.iplot(data, filename: "simple_candlestick");
            }
        }
        static Form1 form = new Form1();
        public static void Main(string[] args)
        {
            form.Show();

            initPyImports();
            const int sampleLength = 56;
            var candles = LoadDataFromFile();
            //DrawChart(candles);
            //ReadLine();

            var prices = candles.Select(x => new[] { x.Close }.Average()).ToArray();
            Console.SetError(new StringWriter());
            double prevPred = 0;
            double prevY = 0;
            double balance = 0;
            double currPred;
            double balanceDiff;
            double[] currPredIntervals;

            var seq = Enumerable.Range(0, 200).Select(i =>
            {
                //for (int i = 0; i < 200; i++)
                //{
                Write(i);

                var sampleX = new ArraySegment<double>(prices, i, sampleLength).ToArray();
                var sampleY = prices[i + sampleLength];
                var stats = WithStopwatch(() => GetBestCorrelations(prices, sampleX, 20), out var time);
                var indexes = stats.best.Select(x => x.index);
                var weights = stats.best.Select(x => x.correlation).ToList();
                var weightsSum = weights.Sum();
                var weightsScaled = weights.Select(x => x / weightsSum).ToList();
                var X = indexes.Select(x => prices.Skip(x).Take(sampleLength).ToList()).ToList();
                var y = indexes.Select(x => prices.Skip(x).Skip(sampleLength).Take(1).ToList()).ToList();
                (currPred, currPredIntervals) = WithStopwatch(() => calcGam(X, y, weightsScaled, sampleX.ToList()), out time);

                return new
                {
                    currPred,
                    candle = candles[i + sampleLength],
                    currY = y[0][0],
                    sampleY,
                    currPredIntervals
                };

                //}
            }).AsParallel();

            var j = 0;
            foreach (var it in seq)
            {
                form.AddCandle(it.candle);
                form.AddIndi(1, it.currPred);
                form.AddIndi(3, it.currPredIntervals);

                if (j > 0)
                {
                    Console.SetCursorPosition(0, 0);
                    balanceDiff = (it.sampleY - prevY) * Math.Sign(it.currPred - prevY) * 100_000;
                    Console.ForegroundColor = ConsoleColor.Green;
                    WriteLine(balance += balanceDiff);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Application.DoEvents();
                }
                prevPred = it.currPred;
                prevY = it.sampleY;
                j++;
            }


            ReadLine();
        }

        public static T[,] CreateMatrix<T>(IList<IList<T>> src)
        {
            var d1 = src.Count;
            var d2 = src[0].Count;

            var t = new T[d1, d2];
            for (int i = 0; i < d1; i++)
            {
                for (int j = 0; j < d2; j++)
                {
                    t[i, j] = src[i][j];
                }
            }
            return t;
        }

        public static TimeSpan WithStopwatch(Action action)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            action();
            sw.Stop();
            return sw.Elapsed;
        }
        public static T WithStopwatch<T>(Func<T> action, out TimeSpan time)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var res = action();
            sw.Stop();
            time = sw.Elapsed;
            return res;
        }

        static void initPyImports()
        {
            using (var py = Py.GIL())
            {
                dynamic gam = Py.Import("pygam");
                dynamic np = Py.Import("numpy");
                dynamic warnings = Py.Import("warnings");
                warnings.filterwarnings("ignore");
            }
        }

        static (double value, double[] intervals) calcGam<T>(List<List<T>> X, List<List<T>> y, List<T> weights, List<T> real)
        {
            //var xyn = X.Zip(y, (x, y1) => (x, y1)).Select(a =>
            //  {
            //      var min = a.x.Min();
            //      var max = a.x.Max();
            //      var rang = (dynamic)max - min;
            //      //var Xn = a.x.Select(x1 => ((dynamic)x1 - min) / rang).Cast<T>().ToList();
            //      //var yn = a.y1.Select(x1 => ((dynamic)x1 - min) / rang).Cast<T>().ToList();
            //      var Xn = a.x.Select(x1 => (dynamic)x1 - rang).Cast<T>().ToList();
            //      var yn = a.y1.Select(x1 => (dynamic)x1 - rang).Cast<T>().ToList();
            //      return (Xn, yn);
            //  }).ToList();

            //X = xyn.Select(x => x.Xn).ToList();
            //y = xyn.Select(x => x.yn).ToList();

            //var rmin = real.Min();
            //var rmax = real.Max();
            //var rrang = (dynamic)rmax - rmin;
            //real = real.Select(x1 => (dynamic)x1 - rrang).Cast<T>().ToList();

            using (var py = Py.GIL())
            {
                dynamic pygam = Py.Import("pygam");
                dynamic numpy = Py.Import("numpy");

                var model = pygam.LinearGAM(n_splines: 6, spline_order: 5, fit_linear: true);

                var fit = model.fit(X: X, y: y, weights: weights);
                var r = new List<List<T>>(new[] { real });
                var res = fit.predict(r)[0];
                //res = ((double[])res).Select(x1 => x1 * rrang + rmin).Cast<double>().ToArray();
                //res = ((double[])res).Select(x1 => x1 + rrang).Cast<double>().ToArray();
                var intervals = model.prediction_intervals(r)[0];

                return (value:(double)res, intervals: (double[])intervals);
            }

        }

        static List<List<T>> Transpose<T>(List<List<T>> inp)
        {
            var d1 = inp[0].Count;
            var d2 = inp.Count;

            var otp = new List<List<T>>(Enumerable.Range(1, d1).Select(x => new List<T>(Enumerable.Range(1, d2).Select(y => default(T)))));
            for (int i = 0; i < d1; i++)
            {
                for (int j = 0; j < d2; j++)
                {
                    otp[i][j] = inp[j][i];
                }
            }
            return otp;
        }

        static void callPy()
        {
            //PythonEngine.PythonPath += @";C:\Gt\StatisticalFrowOnSeries\StatisticalFrowOnSeries\bin\x64\Debug\net461\Lib";

            using (var py = Py.GIL())
            {
                string safeimport(string s)
                {
                    try { Py.Import(s); } catch (Exception ex) { return ex.ToString(); }
                    return "ok";
                }

                dynamic gam = Py.Import("pygam");
                dynamic np = Py.Import("numpy");
                Console.WriteLine(np.cos(np.pi * 2));

                dynamic sin = np.sin;
                Console.WriteLine(sin(5));

                double c = np.cos(5) + sin(5);
                Console.WriteLine(c);

                dynamic a = np.array(new List<float> { 1, 2, 3 });
                Console.WriteLine(a.dtype);

                dynamic b = np.array(new List<float> { 6, 5, 4 }, dtype: np.int32);
                Console.WriteLine(b.dtype);

                Console.WriteLine(a * b);
            }
        }

        private static List<Candle> LoadDataFromFile()
        {
            var lines = File.ReadAllLines(@"EURUSD5.csv");
            var linesColumns = lines.Skip(1).Select(x => x.Split(';', ',')).ToList();
            //var headers = linesColumns[0].Select((x, i) => new { x, i }).ToDictionary(x => x.x, x => x.i);
            return linesColumns.Select(x => new Candle()
            {
                Date = DateTime.ParseExact(x[0] + ' ' + x[1], "yyyy'.'MM'.'dd HH':'mm", CultureInfo.InvariantCulture),
                Open = double.Parse(x[2], CultureInfo.InvariantCulture),
                High = double.Parse(x[3], CultureInfo.InvariantCulture),
                Low = double.Parse(x[4], CultureInfo.InvariantCulture),
                Close = double.Parse(x[5], CultureInfo.InvariantCulture),
                Volume = double.Parse(x[6], CultureInfo.InvariantCulture),
            }).ToList();
        }

        static double[] GetCorelations(double[] series, double[] subseries)
        {
            var precompiled = Precompiled(subseries);
            var l = subseries.Length;
            return Enumerable.Range(0, series.Length - subseries.Length)
                .Select(i => Pearson(new ArraySegment<double>(series, i, l), precompiled))
                .ToArray();
        }

        static
            (
                (int index, double correlation)[] best,
                (int index, double correlation)[] worst
            )
            GetBestCorrelations(double[] series, double[] subseries, int howMany)
        {
            var cors = GetCorelations(series, subseries);
            var list = cors.Select((x, i) => new { i, x }).OrderBy(a => a.x).ToList();
            var down = list.Take(howMany).Select(a => (index: a.i, correlation: a.x)).ToArray();
            list.Reverse();
            var up = list.Where(x => x.x != 1).Take(howMany).Select(a => (index: a.i, correlation: a.x)).ToArray();
            return (best: up, worst: down);
        }

        public static (IList<double> deltas, double var) Precompiled(IEnumerable<double> dataB)
        {
            List<double> deltas = new List<double>();
            double var = 0;

            using (IEnumerator<double> ieB = dataB.GetEnumerator())
            {
                double meanB = 0;
                double varB = 0;
                var n = 0;
                while (ieB.MoveNext())
                {
                    double currentB = ieB.Current;
                    double deltaB = currentB - meanB;
                    deltas.Add(deltaB);
                    double scaleDeltaB = deltaB / ++n;
                    meanB += scaleDeltaB;
                    varB += scaleDeltaB * deltaB * (n - 1);
                }
                var = varB;
            }

            return (deltas.AsReadOnly(), var);
        }

        public static double Pearson(IEnumerable<double> dataA, (IList<double> deltas, double var) precompiled)
        {
            double r = 0.0;

            double meanA = 0;
            double varA = 0;

            var (deltas, var) = precompiled;

            using (IEnumerator<double> ieA = dataA.GetEnumerator())
            using (IEnumerator<double> ieB = deltas.GetEnumerator())
            {
                int n = 0;

                while (ieA.MoveNext() && ieB.MoveNext())
                {
                    double currentA = ieA.Current;

                    double deltaA = currentA - meanA;
                    double scaleDeltaA = deltaA / ++n;


                    meanA += scaleDeltaA;

                    varA += scaleDeltaA * deltaA * (n - 1);
                    r += (deltaA * ieB.Current * (n - 1)) / n;
                }

            }

            return r / Math.Sqrt(varA * var);
        }




    }


}
