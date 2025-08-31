using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LPRDesktopApplication.Models
{
	internal class BranchAndBoundResult
	{
		public double OptimalValue { get; set; }
		public double[] OptimalSolution { get; set; }
		public List<string> IterationLogs { get; set; } = new List<string>();
	}

	internal class BranchAndBound
	{
		private static Dictionary<int, string> _varTypes; // "int", "bin", or ""

		public static BranchAndBoundResult Solve()
		{
			var result = new BranchAndBoundResult();

			try
			{
				// ----------------------
				// Use Relaxed Optimal Tableau
				// ----------------------
				var formattedLines = Program.RelaxedOptimalFormattedLines;
				if (formattedLines == null || formattedLines.Count == 0)
					throw new InvalidOperationException("Relaxed optimal tableau not found. Run PrimalSimplex first.");

				Program.Iterations.Clear();
				Program.OptimalVars.Clear();
				Program.OptimalZValue = "";

				// ----------------------
				// Parse variable constraints
				// ----------------------
				_varTypes = new Dictionary<int, string>();
				int varLineIndex = formattedLines.FindIndex(l => l.StartsWith("Var Constraints:")) + 1;
				if (varLineIndex > 0 && varLineIndex < formattedLines.Count)
				{
					for (int i = varLineIndex; i < formattedLines.Count; i++)
					{
						var varParts = formattedLines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
						foreach (var part in varParts)
						{
							var pair = part.Split(':');
							if (pair.Length == 2 && int.TryParse(pair[0].Substring(1), out int idx))
								_varTypes[idx - 1] = pair[1].ToLower();
						}
					}
				}

				// ----------------------
				// Parse tableau
				// ----------------------
				var tableLines = formattedLines
					.TakeWhile(line => !line.StartsWith("Var Constraints"))
					.Where(line => !string.IsNullOrWhiteSpace(line))
					.ToList();

				var colNames = tableLines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				int cols = colNames.Count;   // include all columns
				int rhsCol = cols - 1;       // last column is RHS
				int rows = tableLines.Count - 1;

				double[,] tableau = new double[rows, cols];
				for (int i = 0; i < rows; i++)
				{
					var rowTokens = tableLines[i + 1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
					var numericTokens = rowTokens.Skip(1).ToArray();
					var row = numericTokens
						.Select(x => double.Parse(x, CultureInfo.InvariantCulture))
						.ToArray();
					for (int j = 0; j < cols; j++)
						tableau[i, j] = row[j];
				}

				// ----------------------
				// Start Branch & Bound
				// ----------------------
				double[] bestSolution = null;
				double bestZ = double.NegativeInfinity;

				Queue<(double[,], string, int)> nodes = new Queue<(double[,], string, int)>();
				nodes.Enqueue(((double[,])tableau.Clone(), "Root", 0));

				int maxNodes = 5000; // safety limit
				int nodeCount = 0;

				while (nodes.Count > 0)
				{
					if (++nodeCount > maxNodes)
					{
						result.IterationLogs.Add("⚠️ Node limit reached, stopping.");
						break;
					}

					var (nodeTableau, nodeName, depth) = nodes.Dequeue();
					string indent = new string(' ', depth * 4);

					// Solve LP relaxation
					double[] solution = DualSimplexSolve(nodeTableau, rhsCol);
					if (solution == null)
					{
						result.IterationLogs.Add($"{indent}{nodeName}: infeasible -> pruned");
						continue;
					}

					double nodeZ = nodeTableau[0, rhsCol];
					result.IterationLogs.Add($"{indent}{nodeName}: Z = {nodeZ:0.##}, sol = [{string.Join(", ", solution.Select(x => x.ToString("0.##", CultureInfo.InvariantCulture)))}]");

					// Prune if worse than best
					if (nodeZ <= bestZ + 1e-6)
					{
						result.IterationLogs.Add($"{indent}{nodeName}: pruned (worse than best {bestZ:0.##})");
						continue;
					}

					// Check integer feasibility
					int branchVar = -1;
					for (int i = 0; i < solution.Length; i++)
					{
						if (_varTypes.ContainsKey(i))
						{
							string type = _varTypes[i];
							if ((type == "bin" || type == "int") && Math.Abs(solution[i] - Math.Round(solution[i])) > 1e-6)
							{
								branchVar = i;
								break;
							}
						}
					}

					if (branchVar == -1)
					{
						// All integer
						if (nodeZ > bestZ)
						{
							bestZ = nodeZ;
							bestSolution = (double[])solution.Clone();
							result.IterationLogs.Add($"{indent}{nodeName}: ✅ new best integer solution Z={bestZ:0.##}");
						}
						continue;
					}

					// Branch on fractional var
					int v = branchVar;
					double floorVal = Math.Floor(solution[v]);
					double ceilVal = Math.Ceiling(solution[v]);

					var left = AddBranchConstraint(nodeTableau, v, floorVal, true, rhsCol);
					result.IterationLogs.Add($"{indent}Added constraint x{v + 1} <= {floorVal}");
					PrintTableau(left);
					nodes.Enqueue((left, $"{nodeName} -> x{v + 1} <= {floorVal}", depth + 1));

					var right = AddBranchConstraint(nodeTableau, v, ceilVal, false, rhsCol);
					result.IterationLogs.Add($"{indent}Added constraint x{v + 1} >= {ceilVal}");
					PrintTableau(right);
					nodes.Enqueue((right, $"{nodeName} -> x{v + 1} >= {ceilVal}", depth + 1));
				}

				// Save results
				result.OptimalSolution = bestSolution ?? new double[cols - 1];
				result.OptimalValue = bestZ;

				// Store in Program globals for UI
				Program.OptimalZValue = result.OptimalValue.ToString("0.##", CultureInfo.InvariantCulture);
				Program.OptimalVars.Clear();
				if (result.OptimalSolution != null)
				{
					for (int i = 0; i < result.OptimalSolution.Length; i++)
						Program.OptimalVars[$"x{i + 1}"] = result.OptimalSolution[i].ToString("0.##", CultureInfo.InvariantCulture);
				}
				Program.Iterations = result.IterationLogs;
			}
			catch (Exception ex)
			{
				result.IterationLogs.Add($"❌ Error: {ex.Message}");
			}

			return result;
		}

		private static double[,] AddBranchConstraint(double[,] tableau, int varIndex, double value, bool lessEqual, int rhsCol)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			double[,] newTableau = new double[rows + 1, cols];
			for (int i = 0; i < rows; i++)
				for (int j = 0; j < cols; j++)
					newTableau[i, j] = tableau[i, j];

			double[] newRow = new double[cols];
			newRow[varIndex] = lessEqual ? 1 : -1;
			newRow[rhsCol] = lessEqual ? value : -value;

			// Adjust new row against basic variables
			for (int i = 1; i < rows; i++)
			{
				for (int j = 0; j < cols - 1; j++)
				{
					if (Math.Abs(tableau[i, j] - 1) < 1e-6 &&
						Enumerable.Range(1, rows - 1).All(r => r == i || Math.Abs(tableau[r, j]) < 1e-6))
					{
						// j is basic in row i
						double factor = newRow[j];
						for (int k = 0; k < cols; k++)
							newRow[k] -= factor * tableau[i, k];
					}
				}
			}

			for (int j = 0; j < cols; j++)
				newTableau[rows, j] = newRow[j];

			return newTableau;
		}

		private static double[] DualSimplexSolve(double[,] tableau, int rhsCol)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			while (true)
			{
				// Find leaving row (most negative RHS)
				int leavingRow = -1;
				double minRHS = 0;
				for (int i = 1; i < rows; i++)
				{
					if (tableau[i, rhsCol] < minRHS)
					{
						minRHS = tableau[i, rhsCol];
						leavingRow = i;
					}
				}
				if (leavingRow == -1) break; // feasible

				// Find entering column
				int pivotCol = -1;
				double minRatio = double.PositiveInfinity;
				for (int j = 0; j < cols - 1; j++)
				{
					if (tableau[leavingRow, j] < 0)
					{
						double ratio = Math.Abs(tableau[0, j] / tableau[leavingRow, j]);
						if (ratio < minRatio)
						{
							minRatio = ratio;
							pivotCol = j;
						}
					}
				}
				if (pivotCol == -1) return null; // infeasible

				// Pivot
				double pivot = tableau[leavingRow, pivotCol];
				for (int j = 0; j < cols; j++)
					tableau[leavingRow, j] /= pivot;

				for (int i = 0; i < rows; i++)
				{
					if (i == leavingRow) continue;
					double factor = tableau[i, pivotCol];
					for (int j = 0; j < cols; j++)
						tableau[i, j] -= factor * tableau[leavingRow, j];
				}
			}

			// Extract solution
			double[] solution = new double[cols - 1];
			for (int j = 0; j < cols - 1; j++)
			{
				double val = 0;
				for (int i = 1; i < rows; i++)
				{
					if (Math.Abs(tableau[i, j] - 1) < 1e-6 &&
						Enumerable.Range(1, rows - 1).All(r => r == i || Math.Abs(tableau[r, j]) < 1e-6))
					{
						val = tableau[i, rhsCol];
						break;
					}
				}
				solution[j] = val;
			}

			return solution;
		}

		private static void PrintTableau(double[,] tableau)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);
			Console.WriteLine("    Tableau:");
			for (int i = 0; i < rows; i++)
			{
				for (int j = 0; j < cols; j++)
					Console.Write($"{tableau[i, j].ToString("0.##", CultureInfo.InvariantCulture)}\t");
				Console.WriteLine();
			}
			Console.WriteLine();
		}
	}
}
