using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LPRDesktopApplication.Models
{
    internal class BranchAndBoundSolver
    {
        public static void Solve(string canonicalFilePath)
        {
            try
            {
                Console.WriteLine($"Starting Solve with file: {canonicalFilePath}");
                string[] lines = File.ReadAllLines(canonicalFilePath);
                Console.WriteLine($"Read {lines.Length} lines from file.");
                if (lines.Length < 4)
                    throw new ArgumentException("Invalid canonical file");

                // Parse header
                string[] header = lines[0].Split('\t');
                Console.WriteLine($"Header: {string.Join(", ", header)}");
                string objectiveType = header[0].ToLower();
                bool isMax = objectiveType == "max";
                if (objectiveType != "max" && objectiveType != "min")
                    throw new ArgumentException("First entry must be 'max' or 'min'.");

                // Extract variable names (excluding objective type and rhs)
                string[] varNames = header.Skip(1).Take(header.Length - 2).ToArray(); // Skip 'max' and 'rhs'
                Console.WriteLine($"Variable names: {string.Join(", ", varNames)}");
                int totalVars = varNames.Length;

                // Parse objective row
                string[] objRow = lines[1].Split('\t');
                Console.WriteLine($"Objective row: {string.Join(", ", objRow)}");
                if (objRow[0] != "z")
                    throw new ArgumentException("Second line must start with 'z'.");
                double[] objCoeffs = objRow.Skip(1).Take(totalVars).Select(double.Parse).ToArray(); // Take only totalVars coefficients
                Console.WriteLine($"Objective coefficients: {string.Join(", ", objCoeffs)}");
                if (objCoeffs.Length != totalVars)
                    throw new ArgumentException("Objective coefficients mismatch.");
                if (double.Parse(objRow.Last()) != 0)
                    throw new ArgumentException("Objective RHS must be 0.");

                // Parse constraints
                int m = lines.Length - 3;
                List<double[]> A = new List<double[]>();
                double[] b = new double[m];
                for (int i = 0; i < m; i++)
                {
                    string[] row = lines[i + 2].Split('\t');
                    Console.WriteLine($"Constraint row {i + 1}: {string.Join(", ", row)}");
                    double[] coeffs = row.Skip(1).Take(totalVars).Select(double.Parse).ToArray(); // Take only totalVars coefficients
                    Console.WriteLine($"Constraint {i + 1} coefficients: {string.Join(", ", coeffs)}");
                    if (coeffs.Length != totalVars)
                        throw new ArgumentException($"Constraint {i + 1} coefficients mismatch.");
                    A.Add(coeffs);
                    b[i] = double.Parse(row.Last());
                    if (b[i] < 0)
                        throw new ArgumentException("RHS must be non-negative.");
                }

                // Parse sign row
                string[] signRow = lines.Last().Split('\t');
                Console.WriteLine($"Sign row: {string.Join(", ", signRow)}");
                if (signRow[0] != "sign")
                    throw new ArgumentException("Last line must start with 'sign'.");
                string[] varConstraints = signRow.Skip(1).ToArray();
                int nOriginal = varConstraints.Length;
                int numSlacks = 0;
                int numSurplus = 0;
                for (int j = nOriginal; j < totalVars; j++)
                {
                    if (varNames[j].StartsWith("s")) numSlacks++;
                    else if (varNames[j].StartsWith("e")) numSurplus++;
                }
                if (nOriginal + numSlacks + numSurplus != totalVars)
                    throw new ArgumentException("Variable count mismatch.");

                // Transform for negative variables
                List<int> integerVars = new List<int>();
                List<bool> isBinary = new List<bool>();
                for (int j = 0; j < nOriginal; j++)
                {
                    string cons = varConstraints[j];
                    if (cons == "-")
                    {
                        objCoeffs[j] = -objCoeffs[j];
                        for (int i = 0; i < m; i++)
                        {
                            A[i][j] = -A[i][j];
                        }
                        varConstraints[j] = "+";
                    }
                    if (cons == "int" || cons == "bin")
                    {
                        integerVars.Add(j);
                        isBinary.Add(cons == "bin");
                    }
                    else if (cons != "+")
                        throw new ArgumentException($"Invalid variable constraint for x{j + 1}.");
                }

                // Initialize best solution tracking
                double bestOpt = isMax ? double.NegativeInfinity : double.PositiveInfinity;
                double[] bestSolution = null;

                // Output file
                string outputFilePath = Path.Combine(Path.GetDirectoryName(canonicalFilePath), "BranchAndBound.txt");
                Console.WriteLine($"Output file path: {outputFilePath}");
                using (StreamWriter writer = new StreamWriter(outputFilePath))
                {
                    writer.WriteLine("Canonical Form:");
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }
                    writer.WriteLine("\nBranch and Bound Process:");

                    // Start backtracking
                    List<BranchConstraint> currentBranches = new List<BranchConstraint>();
                    Backtrack(currentBranches, isMax, objCoeffs, A, b, totalVars, integerVars, isBinary, varNames, writer, ref bestOpt, ref bestSolution, nOriginal);

                    writer.WriteLine("\nBest Candidate:");
                    if (bestSolution != null)
                    {
                        writer.WriteLine($"Optimal Value: {bestOpt}");
                        for (int j = 0; j < nOriginal; j++)
                        {
                            writer.WriteLine($"{varNames[j]} = {bestSolution[j]}");
                        }
                    }
                    else
                    {
                        writer.WriteLine("No feasible integer solution found.");
                    }
                }

                Console.WriteLine($"Output written to {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Solve: {ex.Message}\nStack Trace: {ex.StackTrace}");
                throw; // Re-throw to ensure the outer catch in Main can handle it
            }
        }

        private class BranchConstraint
        {
            public int VarIndex { get; set; }
            public bool IsUpper { get; set; } // true for <=, false for >=
            public double Value { get; set; }
        }

        private static void Backtrack(List<BranchConstraint> branches, bool isMax, double[] obj, List<double[]> AOriginal, double[] bOriginal, int numVarsOriginal, List<int> integerVars, List<bool> isBinary, string[] varNames, StreamWriter writer, ref double bestOpt, ref double[] bestSolution, int nOriginal)
        {
            // Build current LP
            int additional = branches.Count;
            int numVars = numVarsOriginal + additional;
            List<double[]> A = AOriginal.Select(row => row.Concat(Enumerable.Repeat(0.0, additional)).ToArray()).ToList();
            double[] b = (double[])bOriginal.Clone();
            double[] c = obj.Concat(Enumerable.Repeat(0.0, additional)).ToArray();
            List<string> extendedVarNames = varNames.ToList();
            int addedIndex = 0;
            foreach (var br in branches)
            {
                double[] newRow = new double[numVars];
                newRow[br.VarIndex] = 1.0;
                if (br.IsUpper)
                {
                    newRow[numVarsOriginal + addedIndex] = 1.0; // slack
                    extendedVarNames.Add($"branch_s{addedIndex + 1}");
                }
                else
                {
                    newRow[numVarsOriginal + addedIndex] = -1.0; // surplus
                    extendedVarNames.Add($"branch_e{addedIndex + 1}");
                }
                A.Add(newRow);
                b = b.Append(br.Value).ToArray();
                addedIndex++;
            }
            int m = A.Count;

            // Solve LP
            var result = SolveAndPrintTableau(A, b, c, isMax, writer, branches, extendedVarNames.ToArray(), varNames);
            bool feasible = result.Feasible;
            double opt = result.OptValue;
            double[] solution = result.Solution; // size numVars

            if (!feasible)
            {
                writer.WriteLine("Subproblem infeasible. Fathoming.");
                return;
            }

            // Check if all integer
            bool allInteger = true;
            int branchVar = -1;
            double fraction = 0.0;
            double maxFrac = -1.0;
            for (int k = 0; k < integerVars.Count; k++)
            {
                int j = integerVars[k];
                double val = solution[j];
                double dist = Math.Abs(val - Math.Round(val));
                if (dist > 1e-6)
                {
                    allInteger = false;
                    double frac = Math.Min(dist, 1 - dist);
                    if (frac > maxFrac)
                    {
                        maxFrac = frac;
                        branchVar = k;
                        fraction = val;
                    }
                }
            }

            if (allInteger)
            {
                writer.WriteLine("Integer solution found.");
                writer.WriteLine($"Objective: {opt}");
                for (int j = 0; j < nOriginal; j++)
                    writer.WriteLine($"{varNames[j]} = {solution[j]}");

                bool better = isMax ? (opt > bestOpt) : (opt < bestOpt);
                if (better)
                {
                    bestOpt = opt;
                    bestSolution = solution.Take(nOriginal).ToArray();
                }
                writer.WriteLine("Fathoming (optimality).");
                return;
            }

            // Fathom by bound
            if ((isMax && opt <= bestOpt) || (!isMax && opt >= bestOpt))
            {
                writer.WriteLine($"Fathoming by bound. Relaxation opt {opt} not better than best {bestOpt}.");
                return;
            }

            // Branch
            int varIndex = integerVars[branchVar];
            writer.WriteLine($"Branching on {varNames[varIndex]} = {fraction}");
            double floorVal = Math.Floor(fraction);
            double ceilVal = Math.Ceiling(fraction);
            if (isBinary[branchVar])
            {
                floorVal = 0;
                ceilVal = 1;
            }

            // Left: <= floor
            writer.WriteLine($"Left branch: {varNames[varIndex]} <= {floorVal}");
            var left = new List<BranchConstraint>(branches);
            left.Add(new BranchConstraint { VarIndex = varIndex, IsUpper = true, Value = floorVal });
            Backtrack(left, isMax, obj, AOriginal, bOriginal, numVarsOriginal, integerVars, isBinary, varNames, writer, ref bestOpt, ref bestSolution, nOriginal);

            // Right: >= ceil
            writer.WriteLine($"Right branch: {varNames[varIndex]} >= {ceilVal}");
            var right = new List<BranchConstraint>(branches);
            right.Add(new BranchConstraint { VarIndex = varIndex, IsUpper = false, Value = ceilVal });
            Backtrack(right, isMax, obj, AOriginal, bOriginal, numVarsOriginal, integerVars, isBinary, varNames, writer, ref bestOpt, ref bestSolution, nOriginal);
        }

        private class SimplexResult
        {
            public bool Feasible { get; set; }
            public double OptValue { get; set; }
            public double[] Solution { get; set; }
        }

        private static SimplexResult SolveAndPrintTableau(List<double[]> A, double[] b, double[] c, bool isMax, StreamWriter writer, List<BranchConstraint> branches, string[] extendedVarNames, string[] originalVarNames)
        {
            int m = b.Length;
            int n = c.Length; // Number of variables (structural + added aux)
            double[] cUse = (double[])c.Clone();
            if (!isMax)
            {
                for (int j = 0; j < n; j++) cUse[j] = -cUse[j];
            }

            double[,] tableau = new double[m + 1, n + m + 1];
            int[] basic = new int[m];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    tableau[i, j] = A[i][j];
                }
                tableau[i, n + i] = 1.0; // Slack or artificial variables
                tableau[i, n + m] = b[i];
                basic[i] = n + i;
            }

            // Phase 1 objective: max -sum artificials
            for (int j = 0; j < n + m + 1; j++)
                tableau[m, j] = 0.0;
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n + m + 1; j++)
                {
                    tableau[m, j] -= tableau[i, j];
                }
            }

            writer.WriteLine("\nSubproblem: " + (branches.Count == 0 ? "Root" : string.Join(", ", branches.Select(br => originalVarNames[br.VarIndex] + (br.IsUpper ? " <= " : " >= ") + br.Value))));
            writer.WriteLine("Phase 1 start");
            PrintTableau(tableau, m, n, basic, extendedVarNames, writer);

            bool unbounded;
            while (SimplexStep(tableau, m, n + m, basic, out unbounded, writer, extendedVarNames)) // Pass n + m as tableau width
            {
                Console.WriteLine("Phase 1 step completed");
            }

            if (unbounded)
            {
                writer.WriteLine("Unbounded in phase 1.");
                return new SimplexResult { Feasible = false };
            }

            double w = -tableau[m, n + m];
            if (w > 1e-6)
            {
                writer.WriteLine($"Infeasible, w = {w}.");
                return new SimplexResult { Feasible = false };
            }

            // Switch to phase 2
            for (int j = 0; j < n + m + 1; j++)
                tableau[m, j] = 0.0;
            for (int j = 0; j < n; j++)
                tableau[m, j] = -cUse[j];

            // Eliminate basic variables in objective row
            for (int i = 0; i < m; i++)
            {
                int bas = basic[i];
                double factor = tableau[m, bas];
                for (int j = 0; j < n + m + 1; j++)
                {
                    tableau[m, j] -= factor * tableau[i, j];
                }
            }

            writer.WriteLine("Phase 2 start");
            PrintTableau(tableau, m, n, basic, extendedVarNames, writer);

            while (SimplexStep(tableau, m, n + m, basic, out unbounded, writer, extendedVarNames)) // Pass n + m as tableau width
            {
                Console.WriteLine("Phase 2 step completed");
            }

            if (unbounded)
            {
                writer.WriteLine("Unbounded in phase 2.");
                return new SimplexResult { Feasible = false };
            }

            double opt = tableau[m, n + m];
            if (!isMax) opt = -opt;

            double[] solution = new double[n];
            for (int i = 0; i < m; i++)
            {
                int bas = basic[i];
                if (bas < n)
                {
                    solution[bas] = tableau[i, n + m];
                }
            }

            return new SimplexResult { Feasible = true, OptValue = opt, Solution = solution };
        }

        private static bool SimplexStep(double[,] tableau, int m, int tableauWidth, int[] basic, out bool unbounded, StreamWriter writer, string[] extendedVarNames)
        {
            const double epsilon = 1e-6;
            unbounded = false;

            // Debug tableau dimensions
            Console.WriteLine($"Debug: Tableau rows = {tableau.GetLength(0)}, columns = {tableau.GetLength(1)}, tableauWidth = {tableauWidth}");

            // Find entering (Bland's rule: smallest j with positive reduced cost)
            int entering = -1;
            int nStructural = tableauWidth - m; // Number of structural variables
            for (int j = 0; j < nStructural; j++) // Only structural variables
            {
                if (tableau[m, j] > epsilon)
                {
                    if (entering == -1 || j < entering)
                        entering = j;
                }
            }
            if (entering == -1)
                return false;

            Console.WriteLine($"Entering variable: {extendedVarNames[entering]} (index {entering}), reduced cost: {tableau[m, entering]}");

            // Find leaving (Bland's rule: min ratio, smallest i on tie)
            double minRatio = double.PositiveInfinity;
            int leaving = -1;
            List<string> ratioLogs = new List<string>();
            for (int i = 0; i < m; i++)
            {
                if (tableau[i, entering] > epsilon)
                {
                    double ratio = tableau[i, tableauWidth] / tableau[i, entering]; // Use RHS column
                    ratioLogs.Add($"Row {i} ({extendedVarNames[basic[i]]}): ratio = {ratio}, coeff = {tableau[i, entering]}, RHS = {tableau[i, tableauWidth]}");
                    if (ratio < minRatio - epsilon || (Math.Abs(ratio - minRatio) < epsilon && i < leaving))
                    {
                        minRatio = ratio;
                        leaving = i;
                    }
                }
                else
                {
                    ratioLogs.Add($"Row {i} ({extendedVarNames[basic[i]]}): coeff = {tableau[i, entering]} <= 0, no ratio");
                }
            }
            Console.WriteLine("Ratio test results: " + string.Join("; ", ratioLogs));
            if (leaving == -1)
            {
                Console.WriteLine($"No valid leaving variable for entering {extendedVarNames[entering]}, declaring unbounded.");
                unbounded = true;
                return false;
            }

            Console.WriteLine($"Leaving variable: {extendedVarNames[basic[leaving]]} (row {leaving}), ratio: {minRatio}");

            // Validate leaving index
            if (leaving < 0 || leaving >= m)
            {
                Console.WriteLine($"Error: Invalid leaving index {leaving}, declaring unbounded.");
                unbounded = true;
                return false;
            }

            // Pivot
            double pivot = tableau[leaving, entering];
            Console.WriteLine($"Debug: Pivot value = {pivot}, leaving row = {leaving}, entering col = {entering}");
            for (int j = 0; j < tableauWidth; j++) // Process up to last structural/artificial column
            {
                Console.WriteLine($"Debug: Dividing tableau[{leaving}, {j}] = {tableau[leaving, j]} by {pivot}");
                tableau[leaving, j] /= pivot;
            }
            // Handle RHS column separately
            Console.WriteLine($"Debug: Dividing tableau[{leaving}, {tableauWidth}] = {tableau[leaving, tableauWidth]} by {pivot}");
            tableau[leaving, tableauWidth] /= pivot;

            for (int i = 0; i <= m; i++)
            {
                if (i == leaving) continue;
                double factor = tableau[i, entering];
                for (int j = 0; j < tableauWidth; j++)
                {
                    tableau[i, j] -= factor * tableau[leaving, j];
                }
                // Handle RHS column separately
                tableau[i, tableauWidth] -= factor * tableau[leaving, tableauWidth];
            }

            basic[leaving] = entering;

            // Print
            PrintTableau(tableau, m, tableauWidth - m, basic, extendedVarNames, writer);

            return true;
        }

        private static void PrintTableau(double[,] tableau, int m, int n, int[] basic, string[] extendedVarNames, StreamWriter writer)
        {
            writer.WriteLine("Tableau:");
            writer.Write("Basis\t");
            for (int j = 0; j < n; j++)
                writer.Write(extendedVarNames[j] + "\t");
            for (int j = 0; j < m; j++)
                writer.Write($"a{j + 1}\t");
            writer.WriteLine("RHS");

            for (int i = 0; i < m; i++)
            {
                int bas = basic[i];
                string basName = bas < n ? extendedVarNames[bas] : $"a{bas - n + 1}";
                writer.Write(basName + "\t");
                for (int j = 0; j < n + m; j++)
                {
                    writer.Write(tableau[i, j].ToString("F2") + "\t");
                }
                writer.WriteLine(tableau[i, n + m].ToString("F2"));
            }

            writer.Write("obj\t");
            for (int j = 0; j < n + m; j++)
            {
                writer.Write(tableau[m, j].ToString("F2") + "\t");
            }
            writer.WriteLine(tableau[m, n + m].ToString("F2"));
            writer.WriteLine();
        }
    }
}