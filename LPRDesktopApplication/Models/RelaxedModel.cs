using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LPRDesktopApplication.Models
{
	internal class RelaxedModel
	{
		public static void SolveFromFormattedCanonical()
		{
			// Extract table part (before "Var Constraints:")
			var tableLines = Program.FormattedCanonicalFormLines
				.TakeWhile(line => !line.StartsWith("Var Constraints"))
				.Where(line => !string.IsNullOrWhiteSpace(line))
				.ToList();

			if (tableLines.Count < 2)
				throw new InvalidOperationException("Formatted canonical form is missing data.");

			// Column headers
			var colNames = tableLines[0]
				.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.ToList();
			int cols = colNames.Count;

			// Rows
			var rowNames = new List<string>();
			var matrix = new List<double[]>();
			for (int i = 1; i < tableLines.Count; i++)
			{
				var parts = tableLines[i]
					.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
					.ToList();

				rowNames.Add(parts[0]); // keep C1, C2, etc.
				double[] row = parts.Skip(1)
					.Select(p => double.Parse(p, CultureInfo.InvariantCulture))
					.ToArray();
				if (row.Length != cols)
					throw new InvalidOperationException("Row length does not match header length.");
				matrix.Add(row);
			}

			// Convert to 2D array
			int rows = matrix.Count;
			double[,] tableau = new double[rows, cols];
			for (int i = 0; i < rows; i++)
				for (int j = 0; j < cols; j++)
					tableau[i, j] = matrix[i][j];

			// Clear previous data
			Program.Iterations.Clear();
			Program.OptimalVars.Clear();
			Program.OptimalZValue = "";
			Program.RelaxedOptimalFormattedLines.Clear();

			// If any RHS < 0, run dual simplex first
			if (Enumerable.Range(1, rows - 1).Any(r => tableau[r, cols - 1] < 0))
				DualSimplex(tableau, rowNames, colNames);

			// Then run primal simplex
			PrimalSimplex(tableau, rowNames, colNames);

			// Save relaxed optimal tableau
			SaveRelaxedTableau(tableau, rowNames, colNames);
		}

		private static void DualSimplex(double[,] tableau, List<string> rowNames, List<string> colNames)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			while (true)
			{
				int pivotRow = -1;
				double mostNegative = 0;
				for (int i = 1; i < rows; i++)
				{
					if (tableau[i, cols - 1] < mostNegative)
					{
						mostNegative = tableau[i, cols - 1];
						pivotRow = i;
					}
				}
				if (pivotRow == -1) break; // all RHS >= 0

				int pivotCol = -1;
				double minRatio = double.PositiveInfinity;
				for (int j = 0; j < cols - 1; j++)
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

				if (pivotCol == -1)
				{
					Program.Iterations.Add("Dual Simplex: No valid pivot. Problem infeasible.");
					return;
				}

				Pivot(tableau, pivotRow, pivotCol);
			}
		}

		private static void PrimalSimplex(double[,] tableau, List<string> rowNames, List<string> colNames)
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
				if (pivotCol == -1) break; // optimal

				int pivotRow = -1;
				double minRatio = double.PositiveInfinity;
				for (int i = 1; i < rows; i++)
				{
					if (tableau[i, pivotCol] > 0)
					{
						double ratio = tableau[i, cols - 1] / tableau[i, pivotCol];
						if (ratio < minRatio)
						{
							minRatio = ratio;
							pivotRow = i;
						}
					}
				}

				if (pivotRow == -1)
				{
					Program.Iterations.Add("Primal Simplex: Unbounded solution.");
					return;
				}

				Pivot(tableau, pivotRow, pivotCol);
			}

			// Assign optimal vars
			int rhsCol = cols - 1;
			foreach (var col in colNames)
			{
				if (col != "RHS")
				{
					bool isBasic = false;
					int colIndex = colNames.IndexOf(col);
					for (int i = 1; i < rows; i++)
					{
						double val = tableau[i, colIndex];
						if (Math.Abs(val - 1) < 1e-6 &&
							Enumerable.Range(1, rows - 1).All(r => r == i || Math.Abs(tableau[r, colIndex]) < 1e-6))
						{
							Program.OptimalVars[col] = tableau[i, rhsCol].ToString("0.##", CultureInfo.InvariantCulture);
							isBasic = true;
							break;
						}
					}
					if (!isBasic)
						Program.OptimalVars[col] = "0";
				}
			}

			Program.OptimalZValue = tableau[0, rhsCol].ToString("0.##", CultureInfo.InvariantCulture);
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

		private static void SaveRelaxedTableau(double[,] tableau, List<string> rowNames, List<string> colNames)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			var formatted = new List<string>();
			var header = new StringBuilder("      ");
			foreach (var col in colNames)
				header.Append($"{col,6}");
			formatted.Add(header.ToString());

			for (int i = 0; i < rows; i++)
			{
				var row = new StringBuilder($"{rowNames[i],4} ");
				for (int j = 0; j < cols; j++)
					row.Append($"{tableau[i, j].ToString("0.##", CultureInfo.InvariantCulture),6}");
				formatted.Add(row.ToString());
			}

			int varLineIndex = Program.FormattedCanonicalFormLines.FindIndex(l => l.StartsWith("Var Constraints:"));
			if (varLineIndex >= 0)
			{
				formatted.Add("");
				formatted.Add("Var Constraints:");
				for (int i = varLineIndex + 1; i < Program.FormattedCanonicalFormLines.Count; i++)
					formatted.Add(Program.FormattedCanonicalFormLines[i]);
			}

			Program.RelaxedOptimalFormattedLines.AddRange(formatted);
		}
	}
}
