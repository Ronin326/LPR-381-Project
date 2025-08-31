using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LPRDesktopApplication.Models
{
	internal class PrimalSimplex
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

			// First line = column headers
			var colNames = tableLines[0]
				.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.ToList();

			int cols = colNames.Count;

			// Remaining lines = rows
			var rowNames = new List<string>();
			var matrix = new List<double[]>();

			for (int i = 1; i < tableLines.Count; i++)
			{
				var parts = tableLines[i]
					.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
					.ToList();

				rowNames.Add(parts[0]); // Keep as C1, C2, etc.
				double[] row = parts.Skip(1).Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();

				if (row.Length != cols)
					throw new InvalidOperationException("Row length does not match header length.");

				matrix.Add(row);
			}

			int rows = matrix.Count;
			double[,] tableau = new double[rows, cols];
			for (int i = 0; i < rows; i++)
				for (int j = 0; j < cols; j++)
					tableau[i, j] = matrix[i][j];

			Solve(tableau, rowNames, colNames);
		}

		public static void Solve(double[,] tableau, List<string> rowNames, List<string> columnNames)
		{
			Program.Iterations.Clear();
			Program.OptimalVars.Clear();
			Program.OptimalZValue = "";
			Program.RelaxedOptimalFormattedLines.Clear();

			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			// First iteration (initial tableau)
			SaveIteration(tableau, rowNames, columnNames, "T-i");

			int iteration = 0;
			while (true)
			{
				iteration++;
				int pivotCol = ChooseEnteringVariable(tableau);
				if (pivotCol == -1) break; // optimal solution

				int pivotRow = ChooseLeavingVariable(tableau, pivotCol);
				if (pivotRow == -1)
				{
					Program.Iterations.Add("Unbounded solution.");
					return;
				}

				SaveIteration(tableau, rowNames, columnNames,
					$"T-{iteration} Pivot on {columnNames[pivotCol]} and {rowNames[pivotRow]}",
					pivotRow, pivotCol);

				// Perform pivot operation
				Pivot(tableau, pivotRow, pivotCol);
			}

			// Save final optimal tableau
			SaveIteration(tableau, rowNames, columnNames, "T-Optimal");

			// Assign OptimalVars from final tableau
			int rhsCol = cols - 1;

			foreach (var col in columnNames)
			{
				if (col != "RHS")
				{
					bool isBasic = false;
					int colIndex = columnNames.IndexOf(col);

					for (int i = 1; i < rows; i++) // skip z row
					{
						double val = tableau[i, colIndex];

						// Check if column is basic (one 1, rest 0)
						if (Math.Abs(val - 1) < 1e-6 &&
							Enumerable.Range(1, rows - 1).All(r => r == i || Math.Abs(tableau[r, colIndex]) < 1e-6))
						{
							Program.OptimalVars[col] = tableau[i, rhsCol].ToString("0.##");
							isBasic = true;
							break;
						}
					}

					if (!isBasic)
						Program.OptimalVars[col] = "0";
				}
			}

			// Objective value
			Program.OptimalZValue = tableau[0, rhsCol].ToString("0.##");

			// =======================
			// Save Relaxed Optimal Tableau (for B&B)
			// =======================
			var formatted = new List<string>();

			// Header
			var header = new StringBuilder("      ");
			foreach (var col in columnNames)
				header.Append($"{col,6}");
			formatted.Add(header.ToString());

			// Rows
			for (int i = 0; i < rows; i++)
			{
				var row = new StringBuilder($"{rowNames[i],4} ");
				for (int j = 0; j < cols; j++)
					row.Append($"{tableau[i, j],6:0.##}");
				formatted.Add(row.ToString());
			}

			// Variable constraints (copy from original formatted canonical)
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

		private static void SaveIteration(double[,] tableau, List<string> rowNames, List<string> colNames, string title, int pivotRow = -1, int pivotCol = -1)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			var sb = new StringBuilder();
			sb.AppendLine(title);

			// Header
			sb.Append("BV\t");
			foreach (var name in colNames)
				sb.Append($"{name}\t");
			sb.AppendLine();

			// Rows
			for (int i = 0; i < rows; i++)
			{
				sb.Append($"{rowNames[i]}\t");
				for (int j = 0; j < cols; j++)
				{
					string val = tableau[i, j].ToString("0.##").PadLeft(6);
					if (i == pivotRow || j == pivotCol)
						val = $"{val}";
					sb.Append(val + "\t");
				}
				sb.AppendLine();
			}
			sb.AppendLine();

			Program.Iterations.Add(sb.ToString());
		}

		private static int ChooseEnteringVariable(double[,] tableau)
		{
			int cols = tableau.GetLength(1);
			double minValue = 0;
			int pivotCol = -1;

			for (int j = 0; j < cols - 1; j++)
			{
				if (tableau[0, j] < minValue)
				{
					minValue = tableau[0, j];
					pivotCol = j;
				}
			}
			return pivotCol;
		}

		private static int ChooseLeavingVariable(double[,] tableau, int pivotCol)
		{
			int rows = tableau.GetLength(0);
			int rhsCol = tableau.GetLength(1) - 1;

			double minRatio = double.PositiveInfinity;
			int pivotRow = -1;

			for (int i = 1; i < rows; i++) // skip Z row
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
			return pivotRow;
		}

		private static void Pivot(double[,] tableau, int pivotRow, int pivotCol)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);
			double pivot = tableau[pivotRow, pivotCol];

			// Normalize pivot row
			for (int j = 0; j < cols; j++)
				tableau[pivotRow, j] /= pivot;

			// Eliminate pivot column in other rows
			for (int i = 0; i < rows; i++)
			{
				if (i == pivotRow) continue;
				double factor = tableau[i, pivotCol];
				for (int j = 0; j < cols; j++)
					tableau[i, j] -= factor * tableau[pivotRow, j];
			}
		}
	}
}
