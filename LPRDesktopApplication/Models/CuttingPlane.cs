using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LPRDesktopApplication.Models
{
	internal class CuttingPlaneResult
	{
		public double OptimalValue { get; set; }
		public double[] OptimalSolution { get; set; }
		public List<string> IterationLogs { get; set; } = new List<string>();
	}

	internal class CuttingPlane
	{
		private static Dictionary<int, string> _varTypes; // "int", "bin", or ""

		public static CuttingPlaneResult Solve()
		{
			var result = new CuttingPlaneResult();

			try
			{
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
				int cols = colNames.Count;
				int rhsCol = cols - 1;
				int rows = tableLines.Count - 1;

				double[,] tableau = new double[rows, cols];
				for (int i = 0; i < rows; i++)
				{
					var rowTokens = tableLines[i + 1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
					var numericTokens = rowTokens.Skip(1).ToArray();
					var row = numericTokens.Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
					for (int j = 0; j < cols; j++)
						tableau[i, j] = row[j];
				}

				double[] bestSolution = null;
				double bestZ = double.NegativeInfinity;
				int maxIterations = 100;
				int iterationCount = 0;

				while (iterationCount < maxIterations)
				{
					iterationCount++;

					// Solve relaxed LP
					double[] solution = DualSimplexSolve(tableau, rhsCol);
					if (solution == null)
					{
						result.IterationLogs.Add("LP infeasible -> stop.");
						break;
					}

					double nodeZ = tableau[0, rhsCol];
					result.IterationLogs.Add($"Iteration {iterationCount}: Z = {nodeZ:0.##}, sol = [{string.Join(", ", solution.Select(x => x.ToString("0.##", CultureInfo.InvariantCulture)))}]");

					// Check for fractional variables
					int fracVar = -1;
					for (int i = 0; i < solution.Length; i++)
					{
						if (_varTypes.ContainsKey(i))
						{
							string type = _varTypes[i];
							if ((type == "bin" || type == "int") && Math.Abs(solution[i] - Math.Round(solution[i])) > 1e-6)
							{
								fracVar = i;
								break;
							}
						}
					}

					if (fracVar == -1)
					{
						// All integer
						bestSolution = (double[])solution.Clone();
						bestZ = nodeZ;
						result.IterationLogs.Add($"✅ Integer solution found: Z={bestZ:0.##}");
						break;
					}

					// ----------------------
					// Generate Gomory fractional cut
					// ----------------------
					int rowIndex = -1;
					for (int i = 1; i < rows; i++)
					{
						if (Math.Abs(tableau[i, fracVar] - 1) < 1e-6 &&
							Enumerable.Range(1, rows - 1).All(r => r == i || Math.Abs(tableau[r, fracVar]) < 1e-6))
						{
							rowIndex = i;
							break;
						}
					}

					if (rowIndex == -1)
					{
						result.IterationLogs.Add($"Cannot create cut for x{fracVar + 1}, stop.");
						break;
					}

					// Extract fractional parts
					double[] newCut = new double[cols + 1]; // add space for new slack
					for (int j = 0; j < cols - 1; j++)
					{
						double fracPart = tableau[rowIndex, j] - Math.Floor(tableau[rowIndex, j]);
						newCut[j] = -fracPart; // multiply by -1
					}
					double bFrac = tableau[rowIndex, rhsCol] - Math.Floor(tableau[rowIndex, rhsCol]);
					newCut[rhsCol] = -bFrac; // RHS multiplied by -1
					newCut[cols] = 1; // new slack variable

					// Append cut to tableau
					double[,] newTableau = new double[rows + 1, cols + 1];
					for (int i = 0; i < rows; i++)
					{
						for (int j = 0; j < cols; j++)
							newTableau[i, j] = tableau[i, j];
						newTableau[i, cols] = 0; // new slack
					}
					for (int j = 0; j < cols + 1; j++)
						newTableau[rows, j] = newCut[j];

					tableau = newTableau;
					rows++;
					cols++; // increment column count

					result.IterationLogs.Add($"Added Gomory cut for x{fracVar + 1}");
				}

				result.OptimalSolution = bestSolution ?? new double[cols - 1];
				result.OptimalValue = bestZ;

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

		private static double[] DualSimplexSolve(double[,] tableau, int rhsCol)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			while (true)
			{
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
				if (leavingRow == -1) break;

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
				if (pivotCol == -1) return null;

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
	}
}
