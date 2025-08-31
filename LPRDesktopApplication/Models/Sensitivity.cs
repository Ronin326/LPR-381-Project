using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LPRDesktopApplication.Models
{
	internal class Sensitivity
	{
		public static void SolveAndStoreRanges()
		{
			// -------------------
			// Step 1: Parse formatted canonical lines
			// -------------------
			var tableLines = Program.FormattedCanonicalFormLines
				.TakeWhile(line => !line.StartsWith("Var Constraints"))
				.Where(line => !string.IsNullOrWhiteSpace(line))
				.ToList();

			if (tableLines.Count < 2)
				throw new InvalidOperationException("Formatted canonical form is missing data.");

			var colNames = tableLines[0]
				.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.ToList();
			int cols = colNames.Count;

			var rowNames = new List<string>();
			var matrix = new List<double[]>();

			for (int i = 1; i < tableLines.Count; i++)
			{
				var parts = tableLines[i]
					.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
					.ToList();

				rowNames.Add(parts[0]);
				double[] row = parts.Skip(1)
					.Select(p => double.Parse(p.Replace(',', '.'), CultureInfo.InvariantCulture))
					.ToArray();

				if (row.Length != cols)
					throw new InvalidOperationException("Row length does not match header length.");

				matrix.Add(row);
			}

			int rows = matrix.Count;
			double[,] tableau = new double[rows, cols];
			for (int i = 0; i < rows; i++)
				for (int j = 0; j < cols; j++)
					tableau[i, j] = matrix[i][j];

			// -------------------
			// Step 2: Solve with dual + primal simplex
			// -------------------
			DualSimplexSolve(tableau, rowNames, colNames);
			PrimalSimplexSolve(tableau, rowNames, colNames);

			// -------------------
			// Step 3: Identify basic / non-basic vars
			// -------------------
			int rhsCol = cols - 1;

			Program.NBVars.Clear();
			Program.BVars.Clear();
			Program.RHSValues.Clear();

			List<int> basicCols = new List<int>();
			List<int> nonBasicCols = new List<int>();

			for (int j = 0; j < cols - 1; j++)
			{
				string colName = colNames[j];
				bool isBasic = false;
				int basicRow = -1;

				for (int i = 1; i < rows; i++)
				{
					double val = tableau[i, j];
					if (Math.Abs(val - 1) < 1e-6 &&
						Enumerable.Range(1, rows - 1).All(r => r == i || Math.Abs(tableau[r, j]) < 1e-6))
					{
						isBasic = true;
						basicRow = i;
						break;
					}
				}

				if (isBasic)
				{
					basicCols.Add(j);
					Program.BVars[colName] = ""; // placeholder
				}
				else
				{
					nonBasicCols.Add(j);
					Program.NBVars[colName] = "";
				}
			}

			// -------------------
			// Step 4: Compute sensitivity ranges
			// -------------------
			ComputeObjRanges(tableau, basicCols, nonBasicCols, colNames, rhsCol);
			ComputeRHSRanges(tableau, basicCols, rowNames, rhsCol);
		}

		private static void DualSimplexSolve(double[,] tableau, List<string> rowNames, List<string> colNames)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			while (true)
			{
				int pivotRow = -1;
				double mostNegative = 0;
				int rhsCol = cols - 1;

				for (int i = 1; i < rows; i++)
				{
					if (tableau[i, rhsCol] < mostNegative)
					{
						mostNegative = tableau[i, rhsCol];
						pivotRow = i;
					}
				}

				if (pivotRow == -1) break;

				int pivotCol = -1;
				double minRatio = double.PositiveInfinity;

				for (int j = 0; j < rhsCol; j++)
				{
					double coeff = tableau[pivotRow, j];
					double zCost = tableau[0, j];

					if (coeff < 0)
					{
						double ratio = Math.Abs(zCost / coeff);
						if (ratio < minRatio)
						{
							minRatio = ratio;
							pivotCol = j;
						}
					}
				}

				if (pivotCol == -1) break;

				Pivot(tableau, pivotRow, pivotCol);
			}
		}

		private static void PrimalSimplexSolve(double[,] tableau, List<string> rowNames, List<string> colNames)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			while (true)
			{
				int pivotCol = -1;
				double minValue = 0;

				for (int j = 0; j < cols - 1; j++)
				{
					if (tableau[0, j] < minValue)
					{
						minValue = tableau[0, j];
						pivotCol = j;
					}
				}

				if (pivotCol == -1) break;

				int pivotRow = -1;
				double minRatio = double.PositiveInfinity;
				int rhsCol = cols - 1;

				for (int i = 1; i < rows; i++)
				{
					if (tableau[i, pivotCol] > 0)
					{
						double ratio = tableau[i, rhsCol] / tableau[i, pivotCol];
						if (ratio < minRatio)
						{
							minRatio = ratio;
							pivotRow = i;
						}
					}
				}

				if (pivotRow == -1) break;

				Pivot(tableau, pivotRow, pivotCol);
			}
		}

		private static void Pivot(double[,] tableau, int pivotRow, int pivotCol)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);
			double pivot = tableau[pivotRow, pivotCol];

			for (int j = 0; j < cols; j++)
				tableau[pivotRow, j] /= pivot;

			for (int i = 0; i < rows; i++)
			{
				if (i == pivotRow) continue;
				double factor = tableau[i, pivotCol];
				for (int j = 0; j < cols; j++)
					tableau[i, j] -= factor * tableau[pivotRow, j];
			}
		}

		private static void ComputeObjRanges(double[,] tableau, List<int> basicCols, List<int> nonBasicCols, List<string> colNames, int rhsCol)
		{
			// Non-basic vars
			foreach (var j in nonBasicCols)
			{
				double rc = tableau[0, j];
				Program.NBVars[colNames[j]] = $"{rc:0.##} <= {colNames[j]} <= {rc:0.##}";
			}

			// Basic vars
			foreach (var j in basicCols)
			{
				double minInc = double.PositiveInfinity;
				double minDec = double.PositiveInfinity;

				for (int i = 1; i < tableau.GetLength(0); i++)
				{
					if (Math.Abs(tableau[i, j] - 1) < 1e-6)
					{
						for (int k = 0; k < tableau.GetLength(1) - 1; k++)
						{
							if (!basicCols.Contains(k))
							{
								double a = tableau[i, k];
								if (Math.Abs(a) > 1e-6)
								{
									double ratio = tableau[0, k] / a;
									if (a > 0) minInc = Math.Min(minInc, ratio);
									else minDec = Math.Min(minDec, -ratio);
								}
							}
						}
						break;
					}
				}

				Program.BVars[colNames[j]] = $"{-minDec:0.##} <= {colNames[j]} <= {minInc:0.##}";
			}
		}

		private static void ComputeRHSRanges(double[,] tableau, List<int> basicCols, List<string> rowNames, int rhsCol)
		{
			for (int i = 1; i < tableau.GetLength(0); i++)
			{
				double minInc = double.PositiveInfinity;
				double minDec = double.PositiveInfinity;

				for (int j = 0; j < tableau.GetLength(1) - 1; j++)
				{
					if (Math.Abs(tableau[i, j]) > 1e-6)
					{
						double aij = tableau[i, j];
						double ratio = tableau[i, rhsCol] / aij;
						if (aij > 0) minInc = Math.Min(minInc, ratio);
						else minDec = Math.Min(minDec, -ratio);
					}
				}

				Program.RHSValues[rowNames[i]] = $"{-minDec:0.##} <= {rowNames[i]} <= {minInc:0.##}";
			}
		}
	}
}
