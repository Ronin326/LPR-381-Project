using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LPRDesktopApplication
{
    /// <summary>
    /// Standalone reader + solver for the dual that DualBuilder writes.
    /// Expected format (examples):
    ///   min z = 14y1 + 28y2 - 3y3
    ///   1y1 + 4y2 - 1y3 - s1 = -6
    ///   2y1 + 3y2 + 0 - s2 = 0
    ///   + y1 y2 free y3
    ///
    /// Solves the LP: min b^T y  s.t.  A^T y ± s = c,  s >= 0, with y sign line:
    ///     + yk  => yk >= 0
    ///     - yk  => yk <= 0  (we transform as yk = -yk' , yk' >= 0)
    ///     free yk => yk = yk+ - yk- , both >= 0
    ///
    /// Implementation: convert to a MAX form for simplex; basis = identity on slacks
    /// (multiply rows with '-s' by -1 so basis is +1 on slack and RHS >= 0).
    /// </summary>
    public static class DualStandaloneSolver
    {
        #region Public entry points
        private static readonly object _stateLock = new object();

        // Last solved dual variable values (y’s). Empty if nothing solved or last solve failed.
        public static Dictionary<string, double> LastY { get; private set; }
            = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Extra handy bits (optional)
        public static double LastZ { get; private set; } = 0.0;       // last min objective
        public static bool LastSuccess { get; private set; } = false;  // last solve status
        public static DateTime LastSolvedAt { get; private set; }      // timestamp of last attempt
            = DateTime.MinValue;

        // Optional helper if you prefer a copy (read-only-ish)
        public static Dictionary<string, double> GetLastYCopy()
        {
            lock (_stateLock)
            {
                return new Dictionary<string, double>(LastY, StringComparer.OrdinalIgnoreCase);
            }
        }
        public static void SolveDualFromFileAndShow(string fileName = "Dual.txt")
        {
            string full = ResolveOutputPath(fileName);
            if (!File.Exists(full))
            {
                MessageBox.Show($"Dual file not found:\r\n{full}", "Dual Solver", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var lines = File.ReadAllLines(full)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())
                            .ToList();

            var res = SolveDualFromLines(lines);
            ShowResult(res);
        }

        /// <summary>Use this if you already have the dual text in memory.</summary>
        public static void SolveDualFromLinesAndShow(List<string> dualLines)
        {
            var res = SolveDualFromLines(dualLines);
            ShowResult(res);
        }

        #endregion

        #region Core solve

        private sealed class SolveResult
        {
            public bool Success;
            public string Message;
            public Dictionary<string, double> Y;   // original y's in original signs
            public double ZOpt;
            public List<string> Log = new List<string>();
        }

        private enum YSign { Ge0, Le0, Free }

        private sealed class ParsedDual
        {
            public double[] b;               // coefficients in min z = sum b_i * y_i  (i = 1..nY)
            public string[] yNames;          // ["y1", "y2", ...]
            public double[,] A;              // rows = #constraints (mR), cols = nY (coeffs of y)
            public double[] c;               // RHS target for equalities when slacks are zero (i.e., constants on RHS)
            public int mR;                   // # constraints
            public int nY;                   // # y vars
            public int[] slackSign;          // +1 if row has "+ s#", -1 if row had "- s#"
            public Dictionary<string, YSign> ySigns = new Dictionary<string, YSign>(StringComparer.OrdinalIgnoreCase);
        }

        private static SolveResult SolveDualFromLines(List<string> dual)
        {
            var result = new SolveResult { Success = false, Y = new Dictionary<string, double>() };

            try
            {
                var P = ParseDual(dual, result.Log);

                // Build standard-form MAX tableau with nonnegative variables only.
                // Transform y signs: 
                //   y >= 0        => keep column as-is, decision var Yk
                //   y <= 0        => y = -Yk', column = -col, cost adjusts
                //   free          => y = Yk+ - Yk-, duplicate columns (col, -col)
                //
                // Our constraints come as: (row) (sum_j a_ij * y_j) ± s_i = c_i
                // Multiply any row with (-s_i) by -1 to make +s and flip RHS & y columns.
                // Now initial basis: slacks = identity, RHS >= 0 assured by that flip.

                NormalizeSlackSigns(P);

                // Expand columns for sign transforms
                var expand = ExpandColumnsForYSigns(P);

                // Build tableau
                var tab = BuildTableau(P, expand);

                // Run primal simplex (maximize)
                RunSimplex(tab, result.Log);

                // Read optimal in expanded nonnegative variables, then map back to original y
                var yExpanded = ReadNonbasicSolution(tab, expand);  // all variables (expanded y and slacks); nonbasic=0, basic=RHS
                var yOriginal = MapBackToOriginalY(expand, yExpanded);

                result.Y = yOriginal;
                result.ZOpt = ComputeOriginalMinObjective(P, yOriginal); // min b^T y
                result.Success = true;
                result.Message = "Solved";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Log.Add("ERROR: " + ex);
            }

            lock (_stateLock)
            {
                LastSolvedAt = DateTime.Now;
                LastSuccess = result.Success;
                LastZ = result.Success ? result.ZOpt : 0.0;

                LastY.Clear();
                if (result.Success && result.Y != null)
                {
                    foreach (var kv in result.Y)
                        LastY[kv.Key] = kv.Value;
                }
            }

            return result;
        }

        #endregion

        #region Parse dual format

        private static ParsedDual ParseDual(List<string> lines, List<string> log)
        {
            // objective
            int objIdx = lines.FindIndex(s => s.StartsWith("min", StringComparison.OrdinalIgnoreCase) ||
                                              s.StartsWith("max", StringComparison.OrdinalIgnoreCase));
            if (objIdx < 0) throw new Exception("Objective line not found.");
            bool isMin = lines[objIdx].StartsWith("min", StringComparison.OrdinalIgnoreCase);

            if (!isMin) throw new Exception("This quick solver expects a MIN dual objective (got MAX).");

            var (b, yNames) = ParseObjective(lines[objIdx]);

            // constraints: lines with '='
            var cons = new List<string>();
            var signIdx = -1;
            for (int i = objIdx + 1; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("#")) continue;
                if (lines[i].StartsWith("+") || lines[i].StartsWith("-") || lines[i].StartsWith("free", StringComparison.OrdinalIgnoreCase))
                {
                    signIdx = i; break;
                }
                if (lines[i].Contains("=")) cons.Add(lines[i]);
            }
            if (cons.Count == 0) throw new Exception("No constraints found.");

            // sign line
            if (signIdx < 0) throw new Exception("Sign line (e.g., '+ y1 y2 free y3') not found.");
            var ySigns = ParseSignLine(lines[signIdx], yNames);

            // build A, c, slackSign
            int mR = cons.Count;
            int nY = yNames.Length;
            var A = new double[mR, nY];
            var C = new double[mR];
            var slackSign = new int[mR]; // +1 if "+ s", -1 if "- s"

            for (int r = 0; r < mR; r++)
            {
                var row = cons[r];
                var eq = row.LastIndexOf('=');
                if (eq < 0) throw new Exception("Bad constraint: " + row);
                string left = row.Substring(0, eq);
                string rhs = row.Substring(eq + 1);

                // detect slack sign
                slackSign[r] = left.Contains(" - s") ? -1 : +1;

                // fill Y cols
                string leftLow = left.ToLowerInvariant();
                for (int j = 0; j < nY; j++)
                {
                    string yj = yNames[j];
                    A[r, j] = SumCoeffs(leftLow, yj);
                }

                C[r] = ParseDouble(rhs);
            }

            return new ParsedDual
            {
                b = b,
                yNames = yNames,
                A = A,
                c = C,
                mR = mR,
                nY = nY,
                slackSign = slackSign,
                ySigns = ySigns
            };
        }

        private static Tuple<double[], string[]> ParseObjective(string line)
        {
            // "min z = 14y1 + 28y2 - 3y3"
            var rx = new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*y(\d+)", RegexOptions.IgnoreCase);
            var dict = new SortedDictionary<int, double>();
            foreach (Match m in rx.Matches(line))
            {
                var coef = ParseDouble(m.Groups[1].Value);
                var idx = int.Parse(m.Groups[2].Value);
                dict[idx] = dict.ContainsKey(idx) ? dict[idx] + coef : coef;
            }
            if (dict.Count == 0) throw new Exception("No y terms found in objective.");
            var yNames = dict.Keys.Select(k => "y" + k).ToArray();
            var b = dict.Values.ToArray();
            return Tuple.Create(b, yNames);
        }

        private static Dictionary<string, YSign> ParseSignLine(string line, string[] yNames)
        {
            // "+ y1 y2 free y3" or "+ y1 - y2 free y3"
            var res = yNames.ToDictionary(n => n, n => YSign.Ge0, StringComparer.OrdinalIgnoreCase);

            // tokens
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            YSign mode = YSign.Ge0; // default after '+'
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i].ToLowerInvariant();
                if (t == "+") { mode = YSign.Ge0; continue; }
                if (t == "-") { mode = YSign.Le0; continue; }
                if (t == "free") { mode = YSign.Free; continue; }

                if (t.StartsWith("y"))
                {
                    if (res.ContainsKey(tokens[i]))
                        res[tokens[i]] = mode;
                }
            }
            return res;
        }

        #endregion

        #region Build MAX tableau (nonneg vars only)

        private sealed class Expanded
        {
            public List<string> varNames = new List<string>(); // in tableau column order (nonneg variables only)
            public Dictionary<string, int> colIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public List<Tuple<string, string>> freePairs = new List<Tuple<string, string>>(); // (y+, y-) for each free original y
            public Dictionary<string, string> negMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // orig y => new nonneg name when y<=0
            public string[] origY;    // original y names
            public int mR;
        }

        private static void NormalizeSlackSigns(ParsedDual P)
        {
            // For any row with slackSign = -1 (i.e., "- s_i = RHS"), multiply ENTIRE row by -1.
            for (int r = 0; r < P.mR; r++)
            {
                if (P.slackSign[r] == -1)
                {
                    for (int j = 0; j < P.nY; j++) P.A[r, j] = -P.A[r, j];
                    P.c[r] = -P.c[r];
                    P.slackSign[r] = +1;
                }
                // After this, slack column is identity with +1, RHS may change sign.
                // If RHS < 0, tableau is infeasible for primal simplex; but with duals from this builder it typically becomes >=0.
            }
        }

        private static Expanded ExpandColumnsForYSigns(ParsedDual P)
        {
            var E = new Expanded { origY = P.yNames, mR = P.mR };

            // Build columns for each y according to sign:
            //  Ge0:        keep column a[:,j] as is, variable name yj
            //  Le0:        let yj = -yjn  => column becomes -a[:,j], var name yj__neg (and remember mapping yj -> yj__neg)
            //  Free:       yj = yjp - yjn => add two vars with columns (+a[:,j]) and (-a[:,j]), names yj__pos, yj__neg, remember as pair

            for (int j = 0; j < P.nY; j++)
            {
                string yj = P.yNames[j];
                var sign = P.ySigns.ContainsKey(yj) ? P.ySigns[yj] : YSign.Ge0;

                if (sign == YSign.Ge0)
                {
                    AddVar(E, yj);
                }
                else if (sign == YSign.Le0)
                {
                    string yn = yj + "__neg";
                    AddVar(E, yn);
                    E.negMap[yj] = yn;
                    // Column later will be -A[:,j]
                }
                else // Free
                {
                    string yp = yj + "__pos";
                    string yn = yj + "__neg";
                    AddVar(E, yp);
                    AddVar(E, yn);
                    E.freePairs.Add(Tuple.Create(yp, yn));
                }
            }

            // Add slack variables s1..smR at the end (identity columns)
            for (int i = 1; i <= P.mR; i++)
            {
                AddVar(E, "s" + i);
            }

            return E;
        }

        private static void AddVar(Expanded E, string name)
        {
            if (!E.colIdx.ContainsKey(name))
            {
                E.colIdx[name] = E.varNames.Count;
                E.varNames.Add(name);
            }
        }

        private sealed class Tableau
        {
            public double[,] T;       // rows = mR + 1, cols = nVars + 1 (last col = RHS/obj)
            public int mR;            // constraints
            public int nVars;         // decision vars count (expanded y + slacks)
            public int[] basisVar;    // for each row, which column is basic
            public string[] colNames; // variable names by column
        }

        private static Tableau BuildTableau(ParsedDual P, Expanded E)
        {
            int m = P.mR;
            int n = E.varNames.Count;

            var T = new double[m + 1, n + 1]; // last row = obj, last col = RHS
            var basis = new int[m];

            // Fill constraint rows: y-columns first, then slacks I
            for (int r = 0; r < m; r++)
            {
                // RHS
                T[r, n] = P.c[r];

                // y columns (expanded)
                int col = 0;
                for (int j = 0; j < P.nY; j++)
                {
                    string yj = P.yNames[j];
                    var sign = P.ySigns.ContainsKey(yj) ? P.ySigns[yj] : YSign.Ge0;
                    double a = P.A[r, j];

                    if (sign == YSign.Ge0)
                    {
                        int cidx = E.colIdx[yj];
                        T[r, cidx] += a;
                    }
                    else if (sign == YSign.Le0)
                    {
                        string yn = E.negMap[yj]; // y = -yn  => column is -a
                        int cidx = E.colIdx[yn];
                        T[r, cidx] += -a;
                    }
                    else
                    {
                        string yp = yj + "__pos";
                        string yn = yj + "__neg";
                        T[r, E.colIdx[yp]] += a;
                        T[r, E.colIdx[yn]] += -a;
                    }
                }

                // slack identity
                string si = "s" + (r + 1);
                int sIdx = E.colIdx[si];
                T[r, sIdx] = 1.0;
                basis[r] = sIdx;
            }

            // Objective row: we solve MAX problem. Original is MIN: min b^T y.
            // Transform to max: maximize - b^T y (in terms of expanded vars).
            // After sign transforms:
            //  y>=0 : cost for yj is -b_j
            //  y<=0 : y = -yn, so -b*y = -b*(-yn) = +b * yn   (cost = +b_j for yn)
            //  free : y = yp - yn, so -b*y = -b*yp + b*yn   (costs accordingly)
            for (int j = 0; j < P.nY; j++)
            {
                string yj = P.yNames[j];
                var sign = P.ySigns.ContainsKey(yj) ? P.ySigns[yj] : YSign.Ge0;
                double bj = P.b[j];

                if (sign == YSign.Ge0)
                {
                    T[m, E.colIdx[yj]] = -bj;
                }
                else if (sign == YSign.Le0)
                {
                    T[m, E.colIdx[E.negMap[yj]]] = +bj;
                }
                else
                {
                    T[m, E.colIdx[yj + "__pos"]] = -bj;
                    T[m, E.colIdx[yj + "__neg"]] = +bj;
                }
            }

            // Slack costs = 0 by default.

            return new Tableau { T = T, mR = m, nVars = n, basisVar = basis, colNames = E.varNames.ToArray() };
        }

        #endregion

        #region Simplex (maximize)

        private static void RunSimplex(Tableau tab, List<string> log)
        {
            int m = tab.mR;
            int n = tab.nVars;

            // If any RHS < 0 at start (shouldn't after normalization), multiply that row by -1 and flip slack sign—skipped here.

            int iter = 0;
            while (true)
            {
                iter++;
                int enter = -1;
                double best = 1e-12; // only bring columns with positive reduced cost
                for (int j = 0; j < n; j++)
                {
                    double cj = tab.T[m, j];
                    if (cj > best) { best = cj; enter = j; }
                }
                if (enter < 0) break; // optimal

                // Ratio test
                int leave = -1;
                double minRatio = double.PositiveInfinity;
                for (int r = 0; r < m; r++)
                {
                    double a = tab.T[r, enter];
                    if (a > 1e-12)
                    {
                        double ratio = tab.T[r, n] / a;
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leave = r;
                        }
                    }
                }
                if (leave < 0) throw new Exception("Unbounded (no valid leaving variable).");

                Pivot(tab, leave, enter);
                log.Add($"Iter {iter}: enter {tab.colNames[enter]}, leave row {leave + 1}");
            }
        }

        private static void Pivot(Tableau tab, int pivotRow, int pivotCol)
        {
            int m = tab.mR;
            int n = tab.nVars;

            double p = tab.T[pivotRow, pivotCol];
            if (Math.Abs(p) < 1e-12) throw new Exception("Pivot on ~0");

            // Scale pivot row
            for (int j = 0; j <= n; j++) tab.T[pivotRow, j] /= p;

            // Eliminate column elsewhere
            for (int r = 0; r <= m; r++)
            {
                if (r == pivotRow) continue;
                double factor = tab.T[r, pivotCol];
                if (Math.Abs(factor) < 1e-12) continue;
                for (int j = 0; j <= n; j++)
                    tab.T[r, j] -= factor * tab.T[pivotRow, j];
            }

            tab.basisVar[pivotRow] = pivotCol;
        }

        #endregion

        #region Read solution & map back

        private static Dictionary<string, double> ReadNonbasicSolution(Tableau tab, Expanded E)
        {
            // Variables not in basis are zero; RHS gives basic variable values.
            var vals = E.varNames.ToDictionary(n => n, n => 0.0, StringComparer.OrdinalIgnoreCase);

            for (int r = 0; r < tab.mR; r++)
            {
                int c = tab.basisVar[r];
                vals[tab.colNames[c]] = tab.T[r, tab.nVars];
            }

            // Objective: last row RHS is current objective (max of transformed cost).
            // Not needed here; we recompute original min later.

            return vals;
        }

        private static Dictionary<string, double> MapBackToOriginalY(Expanded E, Dictionary<string, double> nonnegVals)
        {
            var y = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var yj in E.origY)
            {
                double val = 0.0;
                if (E.negMap.ContainsKey(yj))
                {
                    // y = - yNeg
                    val = -Get(nonnegVals, E.negMap[yj]);
                }
                else
                {
                    // maybe free: y = y+ - y-
                    string yp = yj + "__pos";
                    string yn = yj + "__neg";
                    if (nonnegVals.ContainsKey(yp) || nonnegVals.ContainsKey(yn))
                        val = Get(nonnegVals, yp) - Get(nonnegVals, yn);
                    else
                        val = Get(nonnegVals, yj);
                }
                y[yj] = val;
            }
            return y;
        }

        private static double ComputeOriginalMinObjective(ParsedDual P, Dictionary<string, double> y)
        {
            double sum = 0.0;
            for (int j = 0; j < P.nY; j++)
            {
                string yj = P.yNames[j];
                sum += P.b[j] * (y.ContainsKey(yj) ? y[yj] : 0.0);
            }
            return sum;
        }

        #endregion

        #region Small utils

        private static string ResolveOutputPath(string fileName)
        {
            string tryRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Output"));
            string altBin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
            return Path.Combine(Directory.Exists(tryRoot) ? tryRoot : altBin, fileName);
        }

        private static double ParseDouble(string s)
        {
            s = s.Trim();
            double v;
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v))
                return v;
            s = s.Replace(" ", "");
            double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);
            return v;
        }

        private static double SumCoeffs(string leftLower, string varName)
        {
            // match "[coef]*varName"  (coef optional sign, decimal, sci)
            var rx = new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*\*?\s*" + Regex.Escape(varName.ToLowerInvariant()) + @"\b",
                               RegexOptions.IgnoreCase);
            double sum = 0.0;
            foreach (Match m in rx.Matches(leftLower))
                sum += ParseDouble(m.Groups[1].Value);
            return sum;
        }

        private static double Get(Dictionary<string, double> d, string key)
        {
            double v; return d.TryGetValue(key, out v) ? v : 0.0;
        }

        private static void ShowResult(SolveResult res)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(res.Success ? "DUAL SOLVE — SUCCESS" : "DUAL SOLVE — FAILED");
            sb.AppendLine();

            if (res.Success)
            {
                sb.AppendLine("y (shadow prices):");
                foreach (var kv in res.Y.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"{kv.Key} = {kv.Value.ToString("0.##########", CultureInfo.InvariantCulture)}");

                sb.AppendLine();
                sb.AppendLine($"z* (min) = {res.ZOpt.ToString("0.##########", CultureInfo.InvariantCulture)}");
            }
            else
            {
                sb.AppendLine(res.Message ?? "(no message)");
            }

            if (res.Log.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Log:");
                foreach (var l in res.Log) sb.AppendLine(l);
            }

            var f = new Form { Text = "Dual Solution", Width = 600, Height = 700 };
            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Consolas", 10),
                Text = sb.ToString()
            };
            f.Controls.Add(tb);
            f.Show();
        }

        #endregion
    }
}
