using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using LPR_381_Project.Utils;   // DualBuilder (for DualCanonicalFormLines)

namespace LPRDesktopApplication
{
    public static class DualityVerifier
    {
        public static void VerifyAndShow(double tol = 1e-6)
        {
            var primalLines = Program.ConicalFormLines ?? new List<string>();   // current model text
            var dualLines = DualBuilder.DualCanonicalFormLines ?? new List<string>();    // built dual text

            // Parse primal
            var P = ParsePrimal(primalLines);
            if (P == null) { Show("Could not parse primal."); return; }

            // Parse dual
            var D = ParseDual(dualLines);
            if (D == null) { Show("Could not parse dual."); return; }

            // Read numeric solutions from Program.OptimalVars (strings -> doubles).
            // Expect keys like "x1", "x2", ... and/or "y1", "y2", ...
            var x = ReadVector(Program.OptimalVars, P.xNames);
            var y = ReadVector(Program.OptimalVars, D.yNames); // if you’ve saved dual y’s into OptimalVars

            // If no dual y’s stored, user might have solved dual separately — just warn.
            bool haveY = GetSize(y) == D.yNames.Length;


            // --- Check feasibility & compute values ---

            // Primal feasibility + slacks
            var slacks = new double[P.m];
            bool primalFeas = true;
            for (int i = 0; i < P.m; i++)
            {
                double Ax = 0.0; for (int j = 0; j < P.n; j++) Ax += P.A[i, j] * x[j];
                switch (P.rowSense[i])
                {
                    case "<=":
                        slacks[i] = P.b[i] - Ax; // >= 0
                        if (slacks[i] < -tol) primalFeas = false;
                        break;
                    case ">=":
                        slacks[i] = Ax - P.b[i]; // >= 0
                        if (slacks[i] < -tol) primalFeas = false;
                        break;
                    default:
                        slacks[i] = Math.Abs(Ax - P.b[i]);
                        if (slacks[i] > tol) primalFeas = false;
                        break;
                }
            }

            // Dual feasibility (in your dual text, constraints are equalities via slack vars; we just check A^T y vs c and signs)
            bool dualFeas = true;
            if (haveY)
            {
                // A^T y vs c
                for (int j = 0; j < P.n; j++)
                {
                    double ATy = 0.0; for (int i = 0; i < P.m; i++) ATy += P.A[i, j] * y[i];
                    // Direction depends on primal sense:
                    // primal MAX with x >= 0 -> A^T y >= c; primal MIN -> A^T y <= c
                    bool ge = (P.objSense == "max");
                    if (ge && (ATy + tol < P.c[j])) dualFeas = false;
                    if (!ge && (ATy - tol > P.c[j])) dualFeas = false;
                }
                // y sign restrictions from primal row sense
                for (int i = 0; i < P.m; i++)
                {
                    if (P.rowSense[i] == "<=" && y[i] < -tol) dualFeas = false; // y_i >= 0
                    if (P.rowSense[i] == ">=" && y[i] > tol) dualFeas = false; // y_i <= 0
                    // '=' -> y free (no check)
                }
            }

            // Objectives
            double zP = 0.0; for (int j = 0; j < P.n; j++) zP += P.c[j] * x[j];
            double zD = double.NaN;
            if (haveY) { for (int i = 0; i < P.m; i++) zD += D.b[i] * y[i]; zD = 0.0; for (int i = 0; i < P.m; i++) zD += P.b[i] * y[i]; }

            // Complementary slackness
            bool csOK = true;
            var csViol = new List<string>();

            if (haveY)
            {
                // (1) Row-wise: y_i * s_i = 0
                for (int i = 0; i < P.m; i++)
                {
                    double prod = Math.Abs(y[i] * ((P.rowSense[i] == "=") ? (0.0) : slacks[i]));
                    if (prod > 1e-5) { csOK = false; csViol.Add($"y{i + 1}*slack{i + 1} ≈ {prod}"); }
                }

                // (2) Column-wise: x_j * reduced_cost_j = 0
                for (int j = 0; j < P.n; j++)
                {
                    double ATy = 0.0; for (int i = 0; i < P.m; i++) ATy += P.A[i, j] * y[i];
                    double rc = (P.objSense == "max") ? (P.c[j] - ATy) : (ATy - P.c[j]);
                    if (Math.Abs(x[j] * rc) > 1e-5) { csOK = false; csViol.Add($"x{j + 1}*rc{j + 1} ≈ {Math.Abs(x[j] * rc)}"); }
                }
            }

            // Report
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("DUALITY VERIFICATION");
            sb.AppendLine("--------------------");
            sb.AppendLine($"Primal objective sense: {P.objSense.ToUpperInvariant()}");
            sb.AppendLine($"Primal feasible: {primalFeas}");
            if (haveY) sb.AppendLine($"Dual feasible: {dualFeas}");
            else sb.AppendLine("Dual feasible: (y-values not provided)");

            // Show objective equality if we have y
            if (haveY)
            {
                double gap = Math.Abs(zP - zD);
                sb.AppendLine($"z_primal = {zP:0.##########}");
                sb.AppendLine($"z_dual   = {zD:0.##########}");
                sb.AppendLine($"|gap|    = {gap:0.##########}  (<= {tol}? {(gap <= tol ? "YES" : "NO")})");
            }
            else
            {
                sb.AppendLine($"z_primal = {zP:0.##########}");
                sb.AppendLine("z_dual   = (unknown — store y1,y2,... in Program.OptimalVars to check strong duality)");
            }

            if (haveY)
            {
                sb.AppendLine();
                sb.AppendLine($"Complementary slackness OK: {csOK}");
                if (!csOK && csViol.Count > 0)
                    sb.AppendLine("Violations: " + string.Join("; ", csViol));
            }

            // Optional: show a hint about where values came from
            sb.AppendLine();
            sb.AppendLine("Note: primal text/solution read from Program globals; dual text from DualBuilder.");
            MessageBox.Show(sb.ToString(), "Duality Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- parsing helpers (primal/dual) ----------

        private sealed class Primal
        {
            public string objSense;      // "max" or "min"
            public double[] c;           // n
            public string[] xNames;      // n
            public double[,] A;          // m x n
            public double[] b;           // m
            public string[] rowSense;    // m entries: "<=", ">=", "=" (from +s, -s, +e)
            public int m, n;
        }

        private sealed class Dual
        {
            public double[] b;           // coefficients in "min z = sum b_i y_i"
            public string[] yNames;      // m
        }
        private static int GetSize(object v)
        {
            if (v == null) return 0;

            if (v is Array arr)
                return arr.Length;

            var type = v.GetType();
            var prop = type.GetProperty("Count");
            if (prop != null)
            {
                var val = prop.GetValue(v, null);
                if (val is int i) return i;
            }

            return 0;
        }

        private static Primal ParsePrimal(List<string> L)
        {
            if (L == null || L.Count == 0) return null;
            var lines = L.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();

            // Find objective (first line starting with max/min)
            int k0 = lines.FindIndex(s => s.StartsWith("max", StringComparison.OrdinalIgnoreCase) ||
                                          s.StartsWith("min", StringComparison.OrdinalIgnoreCase));
            if (k0 < 0) return null;
            string objLine = lines[k0].ToLowerInvariant();
            var objSense = objLine.StartsWith("max") ? "max" : "min";

            // Objective coeffs
            var rxTerm = new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*x(\d+)", RegexOptions.IgnoreCase);
            var map = new SortedDictionary<int, double>();
            foreach (Match k in rxTerm.Matches(objLine))
            {
                int idx = int.Parse(k.Groups[2].Value);
                double coef = ParseD(k.Groups[1].Value);
                map[idx] = map.ContainsKey(idx) ? map[idx] + coef : coef;
            }
            if (map.Count == 0) return null;

            var xNames = map.Keys.Select(k => "x" + k).ToArray();
            var c = map.Values.ToArray();

            // Constraints: lines with '=' after obj
            var cons = new List<string>();
            for (int i = k0 + 1; i < lines.Count; i++)
                if (lines[i].Contains("=")) cons.Add(lines[i]);

            int m = cons.Count, n = xNames.Length;
            var A = new double[m, n];
            var b = new double[m];
            var sense = new string[m];

            var rxX = xNames.Select(name => new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*" + name + @"\b",
                                                      RegexOptions.IgnoreCase)).ToArray();

            for (int i = 0; i < m; i++)
            {
                var row = cons[i];
                int eq = row.LastIndexOf('=');
                var left = row.Substring(0, eq).ToLowerInvariant();
                var rhs = row.Substring(eq + 1);

                // senses: +s -> <=, -s -> >=, +e -> =
                if (left.Contains("+ s")) sense[i] = "<=";
                else if (left.Contains("- s")) sense[i] = ">=";
                else if (left.Contains("+ e")) sense[i] = "=";
                else sense[i] = "=";

                for (int j = 0; j < n; j++)
                {
                    double sum = 0.0;
                    foreach (Match mm in rxX[j].Matches(left))
                        sum += ParseD(mm.Groups[1].Value);
                    A[i, j] = sum;
                }

                b[i] = ParseD(rhs);
            }

            return new Primal { objSense = objSense, c = c, xNames = xNames, A = A, b = b, rowSense = sense, m = m, n = n };
        }

        private static Dual ParseDual(List<string> L)
        {
            if (L == null || L.Count == 0) return null;
            var lines = L.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();

            int k0 = lines.FindIndex(s => s.StartsWith("min", StringComparison.OrdinalIgnoreCase) ||
                                          s.StartsWith("max", StringComparison.OrdinalIgnoreCase));
            if (k0 < 0) return null;

            var rxY = new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*y(\d+)", RegexOptions.IgnoreCase);
            var map = new SortedDictionary<int, double>();
            foreach (Match m in rxY.Matches(lines[k0]))
            {
                int idx = int.Parse(m.Groups[2].Value);
                double coef = ParseD(m.Groups[1].Value);
                map[idx] = map.ContainsKey(idx) ? map[idx] + coef : coef;
            }
            if (map.Count == 0) return null;

            return new Dual { b = map.Values.ToArray(), yNames = map.Keys.Select(k => "y" + k).ToArray() };
        }

        private static double[] ReadVector(Dictionary<string, string> store, string[] names)
        {
            var res = new double[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                double v = 0.0;
                if (store != null && store.TryGetValue(names[i], out var s))
                    double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);
                res[i] = v;
            }
            return res;
        }

        private static double ParseD(string s)
        {
            s = s.Trim();
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
                return v;
            s = s.Replace(" ", "");
            double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);
            return v;
        }

        private static void Show(string msg)
        {
            MessageBox.Show(msg, "Duality Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
