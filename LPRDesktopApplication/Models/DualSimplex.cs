using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LPRDesktopApplication.Models
{
	internal class DualSimplex
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

				rowNames.Add(parts[0]); // keep C1, C2, ...
				double[] row = parts.Skip(1).Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
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

			Solve(tableau, rowNames, colNames);
		}

		public static void Solve(double[,] tableau, List<string> rowNames, List<string> columnNames)
		{
			Program.Iterations.Clear();
			Program.OptimalVars.Clear();
			Program.OptimalZValue = "";

			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			// Save initial tableau
			SaveIteration(tableau, rowNames, columnNames, "T-i");

			int iteration = 0;
			while (true)
			{
				iteration++;

				int pivotRow = ChooseLeavingVariable(tableau); // row with most negative RHS
				if (pivotRow == -1) break; // all RHS >= 0 -> optimal

				int pivotCol = ChooseEnteringVariable(tableau, pivotRow);
				if (pivotCol == -1)
				{
					Program.Iterations.Add("No valid pivot. Problem infeasible.");
					return;
				}

				SaveIteration(tableau, rowNames, columnNames,
					$"T-{iteration} Pivot on {columnNames[pivotCol]} leaving {rowNames[pivotRow]}",
					pivotRow, pivotCol);

				Pivot(tableau, pivotRow, pivotCol);
			}

			// Save final tableau
			SaveIteration(tableau, rowNames, columnNames, "T-Optimal");

			// Extract optimal variables (basic)
			int rhsCol = cols - 1;
			foreach (var col in columnNames)
			{
				if (col != "RHS")
				{
					bool isBasic = false;
					int colIndex = columnNames.IndexOf(col);

					for (int i = 1; i < rows; i++)
					{
						double val = tableau[i, colIndex];
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

			// Objective
			Program.OptimalZValue = tableau[0, rhsCol].ToString("0.##");
		}

		private static int ChooseLeavingVariable(double[,] tableau)
		{
			int rows = tableau.GetLength(0);
			int rhsCol = tableau.GetLength(1) - 1;
			double mostNegative = 0;
			int pivotRow = -1;

			for (int i = 1; i < rows; i++)
			{
				if (tableau[i, rhsCol] < mostNegative)
				{
					mostNegative = tableau[i, rhsCol];
					pivotRow = i;
				}
			}
			return pivotRow;
		}

		private static int ChooseEnteringVariable(double[,] tableau, int pivotRow)
		{
			int cols = tableau.GetLength(1);
			int rhsCol = cols - 1;
			double minRatio = double.PositiveInfinity;
			int pivotCol = -1;

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
			return pivotCol;
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

		private static void SaveIteration(double[,] tableau, List<string> rowNames, List<string> colNames, string title, int pivotRow = -1, int pivotCol = -1)
		{
			int rows = tableau.GetLength(0);
			int cols = tableau.GetLength(1);

			var sb = new StringBuilder();
			sb.AppendLine(title);
			sb.Append("BV\t");
			foreach (var name in colNames) sb.Append($"{name}\t");
			sb.AppendLine();

			for (int i = 0; i < rows; i++)
			{
				sb.Append($"{rowNames[i]}\t");
				for (int j = 0; j < cols; j++)
				{
					string val = tableau[i, j].ToString("0.##").PadLeft(6);
					sb.Append(val + "\t");
				}
				sb.AppendLine();
			}
			sb.AppendLine();

			Program.Iterations.Add(sb.ToString());
		}
	}
}
