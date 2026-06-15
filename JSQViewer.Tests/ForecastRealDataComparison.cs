using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using JSQViewer.Infrastructure.DataImport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    // Diagnostic harness (not a unit test): loads real recordings from the Y: share,
    // builds several candidate dynamics forecasts for the requested triple and measures
    // how far each diverges from the actual measured FULL record. Results -> artifacts.
    [TestClass]
    public class ForecastRealDataComparison
    {
        [TestMethod]
        public void Forecast_Vs_ActualFull_Divergence_NUY45RA()
        {
            RunComparison(
                @"Y:\recordings\Post B\2026\05 Май\14.05.2026 DINAMYC -FORCE PA50 100G FUNC 2560 EDI-P BIG CONDENSER",
                @"Y:\recordings\Post С\2026\05 Май\21.05.2026 DINAMYC -FORCE PA50 100G FULL 3265 EDI-P BIG CONDENSER",
                @"Y:\recordings\Post С\2026\06 Июнь\12.06.2026 DINAMYC -FORCE NUY45RA 90G 3265",
                @"Y:\recordings\Post С\2026\06 Июнь\13.06.2026 DINAMYC -FORCE NUY45RA 90G FULL 3265",
                "nuy45ra");
        }

        [TestMethod]
        public void Forecast_Vs_ActualFull_Divergence_OmegaNpt14()
        {
            RunComparison(
                @"Y:\recordings\Post A\2026\04 Апрель\3.04.2026 OMEGA NPT14 100G FUNC 3265",
                @"Y:\recordings\Post A\2026\04 Апрель\6.04.2026 OMEGA NPT14 100G FULL  3265",
                @"Y:\recordings\Post A\2026\04 Апрель\13.04.2026 OMEGA NPT14 100G FUNC  40ER",
                @"Y:\recordings\Post A\2026\04 Апрель\11.04.2026 OMEGA NPT14 100G FULL  40ER",
                "omega_npt14");
        }

        [TestMethod]
        public void Forecast_Vs_ActualFull_Divergence_Nuy70Ra()
        {
            RunComparison(
                @"Y:\recordings\Post A\2026\04 Апрель\14.04.2026 DINAMYC LS NUY70RA 80 G FUNC 32ER",
                @"Y:\recordings\Post A\2026\04 Апрель\15.04.2026 DINAMYC LS NUY70RA 80 G FULL 32ER",
                @"Y:\recordings\Post A\2026\04 Апрель\24.04.2026 DINAMYC LS NUY70RA 80 G FUNC 40ER",
                @"Y:\recordings\Post A\2026\04 Апрель\22.04.2026 DINAMYC LS NUY70RA 80 G FULL 40ER",
                "nuy70ra");
        }

        private void RunComparison(string oldFuncRoot, string oldFullRoot, string newFuncRoot, string actualFullRoot, string suffix)
        {
            if (!Directory.Exists(actualFullRoot) || !Directory.Exists(newFuncRoot)
                || !Directory.Exists(oldFuncRoot) || !Directory.Exists(oldFullRoot))
            {
                Assert.Inconclusive("Recording share Y: is not available for case " + suffix + ".");
            }

            Series oldFunc = LoadOverlayT1(oldFuncRoot);
            Series oldFull = LoadOverlayT1(oldFullRoot);
            Series newFunc = LoadOverlayT1(newFuncRoot);
            Series actualFull = LoadOverlayT1(actualFullRoot);

            var sb = new StringBuilder();
            sb.AppendLine("=== Forecast vs actual FULL divergence (channel T1) ===");
            sb.AppendLine(Describe("old FUNC", oldFunc));
            sb.AppendLine(Describe("old FULL", oldFull));
            sb.AppendLine(Describe("new FUNC", newFunc));
            sb.AppendLine(Describe("actual FULL", actualFull));
            sb.AppendLine();

            // --- Model 1: current production algorithm ---
            var service = new DynamicsForecastService();
            ChartPipelineSeries production = service.BuildForecast(
                new[]
                {
                    ToPipeline("old-func", "[old FUNC] T1", oldFunc),
                    ToPipeline("old-full", "[old FULL] T1", oldFull),
                    ToPipeline("new-func", "[new FUNC] T1", newFunc)
                },
                new DynamicsForecastRoleSelection("old-func", "old-full", "new-func"));
            Assert.IsNotNull(production);
            var model1 = new Series { X = production.XValues, Y = production.YValues, Column = "current" };

            sb.AppendLine("reference-quality warnings (production default): "
                + (production.ForecastWarnings == null || production.ForecastWarnings.Count == 0
                    ? "none"
                    : string.Join(" | ", production.ForecastWarnings.Select(w => w.Code + "=" + w.Value.ToString("0.##", CultureInfo.InvariantCulture)))));
            sb.AppendLine();

            double startOffset = newFunc.Y[0] - oldFull.Y[0];
            double oldFuncDepth = oldFunc.Y[0] - Min(oldFunc.Y);
            double newFuncDepth = newFunc.Y[0] - Min(newFunc.Y);
            double depthScale = oldFuncDepth > 1e-6 ? newFuncDepth / oldFuncDepth : 1d;

            // --- Model 2: template shift only (no FUNC time-warp) ---
            var model2 = new Series
            {
                X = oldFull.X,
                Y = oldFull.Y.Select(v => v + startOffset).ToArray(),
                Column = "shift-only"
            };

            // --- Model 3: template shift + depth scaling from FUNC excursions ---
            var model3 = new Series
            {
                X = oldFull.X,
                Y = oldFull.Y.Select(v => newFunc.Y[0] - (oldFull.Y[0] - v) * depthScale).ToArray(),
                Column = "shift+depth"
            };

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "startOffset = {0:0.##} C ; oldFuncDepth = {1:0.##} ; newFuncDepth = {2:0.##} ; depthScale = {3:0.###}",
                startOffset, oldFuncDepth, newFuncDepth, depthScale));
            sb.AppendLine();
            sb.AppendLine("model        ; span_h ; n    ; MAE  ; RMSE ; bias ; maxAbs@h");
            sb.AppendLine(EvaluateLine("1 current   ", model1, actualFull));
            sb.AppendLine(EvaluateLine("2 shift-only", model2, actualFull));
            sb.AppendLine(EvaluateLine("3 shift+dpth", model3, actualFull));
            sb.AppendLine();

            sb.AppendLine("--- FUNC warp damping sweep (production algorithm) ---");
            sb.AppendLine("damping ; fullSpan_h ; cmpSpan_h ; n    ; MAE  ; RMSE ; bias ; maxAbs@h");
            foreach (double d in new[] { 1.0, 0.75, 0.5, 0.4, 0.3, 0.25, 0.0 })
            {
                ChartPipelineSeries f = new DynamicsForecastService(d).BuildForecast(
                    new[]
                    {
                        ToPipeline("old-func", "[old FUNC] T1", oldFunc),
                        ToPipeline("old-full", "[old FULL] T1", oldFull),
                        ToPipeline("new-func", "[new FUNC] T1", newFunc)
                    },
                    new DynamicsForecastRoleSelection("old-func", "old-full", "new-func"));
                var fs = new Series { X = f.XValues, Y = f.YValues, Column = "d" };
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,7:0.##} ; {1,10:0.#} ; ",
                    d, f.XValues[f.XValues.Length - 1]) + EvaluateLine("", fs, actualFull).TrimStart(' ', ';'));
            }

            sb.AppendLine();

            double[] sampleHours = { 1, 2, 4, 6, 8, 12, 16, 20, 24, 30, 36, 42 };
            sb.AppendLine("hour ; actual ; m1_current ; m2_shift ; m3_shift+depth");
            foreach (double h in sampleHours)
            {
                double ya;
                if (!TryInterpolate(actualFull, h, out ya))
                {
                    continue;
                }

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,4} ; {1,7:0.##} ; {2,10} ; {3,9} ; {4,9}",
                    h, ya, At(model1, h), At(model2, h), At(model3, h)));
            }

            string artifactsDir = Path.Combine(FindRepoRoot(), "artifacts");
            Directory.CreateDirectory(artifactsDir);
            File.WriteAllText(Path.Combine(artifactsDir, "forecast_compare_" + suffix + ".txt"), sb.ToString(), Encoding.UTF8);
            DumpCsv(Path.Combine(artifactsDir, "forecast_models_" + suffix + ".csv"), actualFull, model1, model2, model3);

            Console.WriteLine(sb.ToString());
        }

        private static string Describe(string name, Series s)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0,-12}: {1} pts, {2:0.##} h, start {3:0.##} C, min {4:0.##} C  (col {5})",
                name, s.X.Length, s.X[s.X.Length - 1], s.Y[0], Min(s.Y), s.Column);
        }

        private static string EvaluateLine(string label, Series forecast, Series actual)
        {
            double forecastMaxX = forecast.X[forecast.X.Length - 1];
            double sumAbs = 0d, sumSq = 0d, sumSigned = 0d, maxAbs = 0d, maxAbsAt = 0d, coveredMax = 0d;
            int n = 0;
            for (int i = 0; i < actual.X.Length; i++)
            {
                double x = actual.X[i];
                if (x < 0d || x > forecastMaxX)
                {
                    continue;
                }

                double yf;
                if (!TryInterpolate(forecast, x, out yf))
                {
                    continue;
                }

                double err = yf - actual.Y[i];
                double abs = Math.Abs(err);
                sumAbs += abs;
                sumSq += err * err;
                sumSigned += err;
                if (abs > maxAbs) { maxAbs = abs; maxAbsAt = x; }
                if (x > coveredMax) coveredMax = x;
                n++;
            }

            if (n == 0)
            {
                return label + " ; no overlap";
            }

            return string.Format(CultureInfo.InvariantCulture,
                "{0} ; {1,5:0.#} ; {2,4} ; {3,4:0.##} ; {4,4:0.##} ; {5,5:0.##} ; {6:0.#}@{7:0.#}",
                label, coveredMax, n, sumAbs / n, Math.Sqrt(sumSq / n), sumSigned / n, maxAbs, maxAbsAt);
        }

        private static string At(Series s, double h)
        {
            double y;
            return TryInterpolate(s, h, out y) ? y.ToString("0.##", CultureInfo.InvariantCulture) : "-";
        }

        private static void DumpCsv(string path, Series actual, Series m1, Series m2, Series m3)
        {
            var rows = new List<string> { "x_hours;actual;m1_current;m2_shift;m3_shift_depth" };
            for (int i = 0; i < actual.X.Length; i += 5)
            {
                double x = actual.X[i];
                rows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0:0.####};{1:0.###};{2};{3};{4}",
                    x, actual.Y[i], At(m1, x), At(m2, x), At(m3, x)));
            }

            File.WriteAllText(path, string.Join(Environment.NewLine, rows), Encoding.UTF8);
        }

        private static double Min(double[] a)
        {
            double m = double.PositiveInfinity;
            for (int i = 0; i < a.Length; i++) if (a[i] < m) m = a[i];
            return m;
        }

        private static bool TryInterpolate(Series s, double x, out double y)
        {
            y = 0d;
            double[] xs = s.X;
            double[] ys = s.Y;
            if (xs.Length == 0 || x < xs[0] || x > xs[xs.Length - 1])
            {
                return false;
            }

            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (xs[mid] <= x) lo = mid; else hi = mid;
            }

            double x0 = xs[lo], x1 = xs[hi];
            if (Math.Abs(x1 - x0) < 1e-12) { y = ys[lo]; return true; }
            double t = (x - x0) / (x1 - x0);
            y = ys[lo] + (ys[hi] - ys[lo]) * t;
            return true;
        }

        private static ChartPipelineSeries ToPipeline(string code, string legend, Series s)
        {
            return new ChartPipelineSeries { Code = code, LegendText = legend, SourceRoot = code, XValues = s.X, YValues = s.Y };
        }

        private static Series LoadOverlayT1(string root)
        {
            var reader = new DbfTestDataSourceReader();
            TestData data = reader.Read(root, new Dictionary<string, ChannelInfo>(), new Dictionary<string, string>());
            string column = PickT1Column(data, root);
            long[] ts = data.TimestampsMs;
            double?[] col = data.Columns[column];
            long baseMs = ts[0];

            var xs = new List<double>(ts.Length);
            var ys = new List<double>(ts.Length);
            int n = Math.Min(ts.Length, col.Length);
            for (int i = 0; i < n; i++)
            {
                if (!col[i].HasValue) continue;
                double v = col[i].Value;
                if (v <= -90.0) continue;
                xs.Add(Math.Max(0L, ts[i] - baseMs) / 3600000.0);
                ys.Add(v);
            }

            return new Series { X = xs.ToArray(), Y = ys.ToArray(), Column = column };
        }

        private static string PickT1Column(TestData data, string root)
        {
            foreach (string preferred in new[] { "C-T1", "B-T1" })
            {
                if (data.Columns.ContainsKey(preferred) && HasData(data.Columns[preferred])) return preferred;
            }

            foreach (string name in data.ColumnNames)
            {
                string trimmed = name.Trim();
                if ((trimmed.EndsWith("-T1", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("T1", StringComparison.OrdinalIgnoreCase))
                    && HasData(data.Columns[name])) return name;
            }

            throw new InvalidOperationException("No T1 column with data in " + root + ". Columns: " + string.Join(", ", data.ColumnNames));
        }

        private static bool HasData(double?[] col)
        {
            for (int i = 0; i < col.Length; i++) if (col[i].HasValue && col[i].Value > -90.0) return true;
            return false;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "JSQViewer.csproj"))) dir = dir.Parent;
            return dir != null ? dir.FullName : AppDomain.CurrentDomain.BaseDirectory;
        }

        private sealed class Series
        {
            public double[] X;
            public double[] Y;
            public string Column;
        }
    }
}
