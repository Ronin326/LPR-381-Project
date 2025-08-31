using LPRDesktopApplication;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LPR_381_Project.Utils
{
    public static class DualBuilder
    {

        // Stored output dual (same canonical style)
        public static List<string> DualCanonicalFormLines = new List<string>();

        // Optional: messages collected during parse/build
        public static List<string> LastWarnings = new List<string>();

        private enum Sense { Max, Min }
        private enum ConstrSense { LE, GE, EQ }

        private sealed class PrimalModel
        {
            public Sense ObjSense;
            public double[] c;                 // objective coeffs for x1..xn (after eliminating fixed-zero vars)
            public string[] xNames;            // kept x variables (after elimination)
            public double[,] A;                // m x n (rows = constraints, cols = kept variables)
            public double[] b;                 // RHS (m)
            public ConstrSense[] rowSense;     // inferred from +s / -s / +e
            public int m;                      // constraints
            public int n;                      // kept variable count
            public bool HasIntFlag;
        }

        private sealed class VariableConstraintInfo
        {
            public HashSet<string> FixedZero = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Integer = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Main entry: parse primal from FormattedCanonicalFormLines (+ variable constraints),
        /// build dual, store in DualCanonicalFormLines.
        /// </summary>
        public static void BuildAndStoreDualFromCurrent()
        {
            LastWarnings.Clear();
            DualCanonicalFormLines.Clear();
            foreach(string line in Program.FormattedCanonicalFormLines)
            {
               Console.WriteLine(line);
            }
            if (Program.ConicalFormLines == null || Program.ConicalFormLines.Count == 0)
            {
                LastWarnings.Add("No canonical form lines found (Program.FormattedCononicalLines is empty).");
                return;
            }

            var model = ParsePrimalWithVarConstraints(Program.ConicalFormLines);
            if (model == null)
            {
                LastWarnings.Add("Failed to parse primal canonical form.");
                return;
            }

            var dual = BuildDual(model);
            DualCanonicalFormLines = dual;
        }

        // -------- Parsing (with Variable Constraints block) --------

        private static PrimalModel ParsePrimalWithVarConstraints(List<string> rawLines)
        {
            // Clean & trim, preserve order
            var L = rawLines
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (L.Count < 2) return null;

            // Split into "canonical core" vs "variable constraints" sections
            var vcStart = L.FindIndex(s => s.StartsWith("Variable Constraints", StringComparison.OrdinalIgnoreCase));
            var canonicalLines = (vcStart >= 0) ? L.Take(vcStart).ToList() : L;
            var vcLines = (vcStart >= 0) ? L.Skip(vcStart).ToList() : new List<string>();

            var vcInfo = ParseVariableConstraints(vcLines);

            // 1) Objective line
            var objLineOrig = canonicalLines[0];
            var objLine = objLineOrig.ToLowerInvariant();

            Sense sense;
            if (objLine.StartsWith("max")) sense = Sense.Max;
            else if (objLine.StartsWith("min")) sense = Sense.Min;
            else return null;

            // Extract objective coeffs: values on x1..xn
            var xCoeff = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase); // name -> coeff
            var xCoeffFoundOrder = new List<string>(); // to preserve discovery order if needed

            var eqIdx = objLine.IndexOf('=');
            var rhsExpr = eqIdx >= 0 ? objLine.Substring(eqIdx + 1) : objLine;

            ExtractCoeffsInto(rhsExpr, ref xCoeff, ref xCoeffFoundOrder);

            if (xCoeff.Count == 0) return null;

            // Build xNames in ascending index order
            var xNamesAll = xCoeff.Keys
                .Select(name => new { name, idx = SafeIndex(name) })
                .OrderBy(p => p.idx)
                .Select(p => p.name)
                .ToArray();

            // 2) Constraint lines up to "sign/bounds" or end (before Variable Constraints section, already split)
            var constraints = new List<string>();
            int i = 1;
            for (; i < canonicalLines.Count; i++)
            {
                var line = canonicalLines[i];
                if (line.StartsWith("+ ") || line.StartsWith("sign", StringComparison.OrdinalIgnoreCase) || line.StartsWith("bounds", StringComparison.OrdinalIgnoreCase))
                    break;
                if (line.Contains("=")) constraints.Add(line);
            }

            // 3) Optional sign/bounds line (check if 'int' appears)
            bool hasInt = false;
            for (; i < canonicalLines.Count; i++)
            {
                var low = canonicalLines[i].ToLowerInvariant();
                if (low.Contains(" int ")) hasInt = true;
            }

            // Merge with variable-constraint integers
            if (vcInfo.Integer.Count > 0) hasInt = true;

            // Parse constraints: A, b, rowSense (using xNamesAll)
            int m = constraints.Count;
            var tmpA = new double[m, xNamesAll.Length];
            var b = new double[m];
            var rowSense = new ConstrSense[m];

            var sPlus = new Regex(@"\+\s*s\d+", RegexOptions.IgnoreCase);
            var sMinus = new Regex(@"\-\s*s\d+", RegexOptions.IgnoreCase);
            var ePlus = new Regex(@"\+\s*e\d+", RegexOptions.IgnoreCase);

            for (int r = 0; r < m; r++)
            {
                var raw = constraints[r];

                var eq = raw.LastIndexOf('=');
                if (eq < 0) throw new Exception("Constraint without '=': " + raw);
                var left = raw.Substring(0, eq);
                var rhs = raw.Substring(eq + 1);

                var leftLower = left.ToLowerInvariant();
                // Fill A for all xNamesAll
                for (int j = 0; j < xNamesAll.Length; j++)
                {
                    var name = xNamesAll[j];
                    var rx = new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*\*?\s*" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase);
                    double sum = 0.0;
                    foreach (Match mco in rx.Matches(leftLower))
                        sum += ParseDoubleSafe(mco.Groups[1].Value);
                    tmpA[r, j] = sum;
                }

                b[r] = ParseDoubleSafe(rhs);

                if (ePlus.IsMatch(leftLower))
                    rowSense[r] = ConstrSense.EQ;
                else if (sPlus.IsMatch(leftLower))
                    rowSense[r] = ConstrSense.LE;
                else if (sMinus.IsMatch(leftLower))
                    rowSense[r] = ConstrSense.GE;
                else
                {
                    rowSense[r] = ConstrSense.EQ;
                    LastWarnings.Add($"Constraint {r + 1}: could not infer sense from slack/surplus/equality marker. Assuming '='.");
                }
            }

            // 4) Apply variable eliminations from "Variable Constraints:"
            // - Any "xk = 0" → drop that variable entirely (remove its column and objective coeff).
            var columnsToKeep = new List<int>();
            var keptNames = new List<string>();
            var keptC = new List<double>();

            for (int j = 0; j < xNamesAll.Length; j++)
            {
                var nm = xNamesAll[j];
                if (vcInfo.FixedZero.Contains(nm))
                {
                    // Eliminate: objective loses c_j term; constraints drop column
                    // (No RHS update needed for =0)
                    continue;
                }
                columnsToKeep.Add(j);
                keptNames.Add(nm);
                keptC.Add(xCoeff.ContainsKey(nm) ? xCoeff[nm] : 0.0);
            }

            var nKept = keptNames.Count;
            var A = new double[m, nKept];
            for (int r = 0; r < m; r++)
            {
                for (int jj = 0; jj < nKept; jj++)
                {
                    A[r, jj] = tmpA[r, columnsToKeep[jj]];
                }
            }

            if (vcInfo.FixedZero.Count > 0)
            {
                var dropped = string.Join(", ", vcInfo.FixedZero.OrderBy(n => SafeIndex(n)));
                LastWarnings.Add($"Eliminated fixed-zero variables: {dropped}.");
            }

            if (vcInfo.Integer.Count > 0)
            {
                var ints = string.Join(", ", vcInfo.Integer.OrderBy(n => SafeIndex(n)));
                LastWarnings.Add($"Integer variables detected (treated as continuous in dual): {ints}.");
            }

            if (hasInt)
            {
                LastWarnings.Add("Note: Dual is constructed for the continuous relaxation.");
            }

            return new PrimalModel
            {
                ObjSense = sense,
                c = keptC.ToArray(),
                xNames = keptNames.ToArray(),
                A = A,
                b = b,
                rowSense = rowSense,
                m = m,
                n = nKept,
                HasIntFlag = hasInt
            };
        }

        private static VariableConstraintInfo ParseVariableConstraints(List<string> vcLines)
        {
            var info = new VariableConstraintInfo();
            if (vcLines == null || vcLines.Count == 0) return info;

            // Expect a header like "Variable Constraints:" followed by one or more lines.
            // We’ll scan everything after the header for patterns:
            // - xk = 0
            // - xk E Z  (integer)
            var start = vcLines.FindIndex(s => s.StartsWith("Variable Constraints", StringComparison.OrdinalIgnoreCase));
            if (start < 0) return info;

            var blob = string.Join(" ", vcLines.Skip(start + 1));
            // Tokenize by ';' and spaces
            // Scan with regex:
            //  1) fixed zero:  (x\d+)\s*=\s*0
            //  2) integer:     (x\d+)\s*E\s*Z   or   (x\d+)\s*∈\s*Z (support Unicode ∈ if ever present)
            var reFixedZero = new Regex(@"\b(x\d+)\s*=\s*0\b", RegexOptions.IgnoreCase);
            var reInteger1 = new Regex(@"\b(x\d+)\s*E\s*Z\b", RegexOptions.IgnoreCase);
            var reInteger2 = new Regex(@"\b(x\d+)\s*∈\s*Z\b", RegexOptions.IgnoreCase);

            foreach (Match m in reFixedZero.Matches(blob))
                info.FixedZero.Add(m.Groups[1].Value);

            foreach (Match m in reInteger1.Matches(blob))
                info.Integer.Add(m.Groups[1].Value);

            foreach (Match m in reInteger2.Matches(blob))
                info.Integer.Add(m.Groups[1].Value);

            return info;
        }

        private static void ExtractCoeffsInto(string expr, ref Dictionary<string, double> into, ref List<string> discoveredOrder)
        {
            // Match "coef * xK" allowing spaces; also allow "coefxK"
            var rxTight = new Regex(@"([+\-]?\s*\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*\*?\s*(x\d+)\b", RegexOptions.IgnoreCase);
            foreach (Match m in rxTight.Matches(expr))
            {
                var coef = ParseDoubleSafe(m.Groups[1].Value);
                var name = m.Groups[2].Value;
                if (!into.ContainsKey(name))
                {
                    into[name] = 0.0;
                    discoveredOrder.Add(name);
                }
                into[name] += coef;
            }

            // Fallback for concatenated like "-3x1" without clear separator (already covered but keep)
            var rxLoose = new Regex(@"([+\-]?\d*\.?\d+(?:[eE][+\-]?\d+)?)\s*(x\d+)\b", RegexOptions.IgnoreCase);
            foreach (Match m in rxLoose.Matches(expr))
            {
                var coef = ParseDoubleSafe(m.Groups[1].Value);
                var name = m.Groups[2].Value;
                if (!into.ContainsKey(name))
                {
                    into[name] = 0.0;
                    discoveredOrder.Add(name);
                }
                into[name] += coef;
            }
        }

        private static int SafeIndex(string xName)
        {
            var digits = new string(xName.SkipWhile(ch => !char.IsDigit(ch)).ToArray());
            int k;
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out k) ? k : int.MaxValue;
        }

        private static double ParseDoubleSafe(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0.0;
            s = s.Trim();
            double v;
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v))
                return v;

            s = s.Replace(" ", "");
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v))
                return v;

            return 0.0;
        }

        // -------- Dual construction --------

        private static List<string> BuildDual(PrimalModel P)
        {
            // If no variables remain (all fixed-zero), dual has only an objective and no dual constraints.
            var dual = new List<string>();

            // Build dual objective: b^T y with flipped sense
            var yNames = Enumerable.Range(1, P.m).Select(k => "y" + k).ToArray();
            var objSenseDual = (P.ObjSense == Sense.Max) ? "min" : "max";

            var objBuilder = new StringBuilder();
            objBuilder.Append(objSenseDual).Append(" z = ");
            for (int i = 0; i < P.m; i++)
            {
                var term = CoefTerm(P.b[i], yNames[i], first: i == 0);
                objBuilder.Append(term);
            }
            dual.Add(objBuilder.ToString());

            // Dual constraints: A^T y (size n) related to c
            bool dualIsGE = (P.ObjSense == Sense.Max); // if true, A^T y >= c; else A^T y <= c
            int slackCount = 0;

            for (int j = 0; j < P.n; j++)
            {
                var lhs = new StringBuilder();
                for (int i = 0; i < P.m; i++)
                {
                    double aij = P.A[i, j];
                    if (i == 0) lhs.Append(CoefTerm(aij, yNames[i], true));
                    else lhs.Append(CoefTerm(aij, yNames[i], false));
                }

                string constraint;
                if (dualIsGE)
                {
                    // >= → equality via -s
                    slackCount++;
                    constraint = $"{lhs} - s{slackCount} = {Num(P.c[j])}";
                }
                else
                {
                    // <= → equality via +s
                    slackCount++;
                    constraint = $"{lhs} + s{slackCount} = {Num(P.c[j])}";
                }
                dual.Add(constraint);
            }

            // Sign restrictions for y
            var signLine = new StringBuilder();
            signLine.Append("+");
            bool anyPrinted = false;
            for (int i = 0; i < P.m; i++)
            {
                switch (P.rowSense[i])
                {
                    case ConstrSense.LE:
                        signLine.Append(" ").Append(yNames[i]);        // y_i >= 0
                        anyPrinted = true;
                        break;
                    case ConstrSense.GE:
                        signLine.Append(" - ").Append(yNames[i]);       // y_i <= 0
                        anyPrinted = true;
                        break;
                    case ConstrSense.EQ:
                        signLine.Append(" free ").Append(yNames[i]);    // y_i free
                        anyPrinted = true;
                        break;
                }
            }
            if (!anyPrinted) signLine.Append(" (no explicit sign restrictions inferred for y)");
            dual.Add(signLine.ToString());

            if (P.HasIntFlag)
                dual.Add("# Note: primal had integer markers; dual built for the continuous relaxation.");

            return dual;
        }

        // -------- Helpers --------

        private static string Num(double v)
        {
            return v.ToString("0.##########", CultureInfo.InvariantCulture);
        }

        private static string CoefTerm(double coef, string varName, bool first)
        {
            if (Math.Abs(coef) < 1e-12) return first ? "0" : " + 0";
            var abs = Math.Abs(coef);
            var mag = abs.ToString("0.##########", CultureInfo.InvariantCulture);
            if (first)
            {
                return (coef < 0 ? "-" : "") + mag + varName;
            }
            else
            {
                return (coef < 0 ? " - " : " + ") + mag + varName;
            }
        }
    }
}
