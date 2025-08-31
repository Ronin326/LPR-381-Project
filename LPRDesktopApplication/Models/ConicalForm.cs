using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LPRDesktopApplication.Models
{
	internal class ConicalForm
	{
		private class LinearProgram
		{
			public bool IsMaximization { get; set; }
			public double[] ObjectiveCoefficients { get; set; }
			public List<double[]> Constraints { get; set; } = new();
			public List<string> Relations { get; set; } = new();
			public List<double> RHS { get; set; } = new();
			public string[] SignRestrictions { get; set; }
		}

		public static void GenerateConicalForm(string path)
		{
			var lines = File.ReadAllLines(path)
				.Where(l => !string.IsNullOrWhiteSpace(l))
				.Select(l => l.Trim())
				.ToArray();

			if (lines.Length < 3)
				throw new InvalidOperationException("Expected: objective line, >=1 constraint line, and a sign-restrictions line.");

			LinearProgram lp = new LinearProgram();
			lp.IsMaximization = lines[0].Contains("max");

			// Parse objective coefficients
			var coefficients = lines[0]
				.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.Where(c => c != "max" && c != "min")
				.Select(c => double.Parse(c, CultureInfo.InvariantCulture))
				.ToArray();
			lp.ObjectiveCoefficients = coefficients;

			// Parse constraints
			for (int i = 1; i < lines.Length - 1; i++)
			{
				var parts = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

				if (!double.TryParse(parts.Last(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rhs))
					continue; // skip non-numeric lines (var constraints)

				var coeffs = parts.Take(parts.Length - 2)
								  .Select(c => double.Parse(c, CultureInfo.InvariantCulture))
								  .ToArray();

				lp.Constraints.Add(coeffs);
				lp.Relations.Add(parts[parts.Length - 2]);
				lp.RHS.Add(rhs);
			}

			// Last line = variable restrictions
			lp.SignRestrictions = lines[lines.Length - 1]
				.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
				.ToArray();

			// =====================
			// Canonical Form Display
			// =====================
			Program.ConicalFormLines.Clear();

			string objLine = lp.IsMaximization ? "max z = " : "min z = ";
			for (int i = 0; i < lp.ObjectiveCoefficients.Length; i++)
			{
				double coef = lp.ObjectiveCoefficients[i];
				objLine += i == 0
					? $"-{coef.ToString(CultureInfo.InvariantCulture)}x{i + 1}"
					: $" - {coef.ToString(CultureInfo.InvariantCulture)}x{i + 1}";
			}
			Program.ConicalFormLines.Add(objLine);

			int k = 1; // for slack/artificial numbering
			for (int i = 0; i < lp.Constraints.Count; i++)
			{
				var coeffs = (double[])lp.Constraints[i].Clone();
				double rhs = lp.RHS[i];
				string rel = lp.Relations[i];
				string line = "";

				if (rel == ">=")
				{
					for (int j = 0; j < coeffs.Length; j++)
						coeffs[j] *= -1;
					rhs *= -1;
				}

				for (int j = 0; j < coeffs.Length; j++)
				{
					line += j == 0
						? $"{coeffs[j]}x{j + 1}"
						: $" {(coeffs[j] >= 0 ? "+" : "-")} {Math.Abs(coeffs[j])}x{j + 1}";
				}

				// Slack or artificial variable
				line += rel == ">=" ? $" + e{k} = {rhs}" : $" + s{k} = {rhs}";
				k++;
				Program.ConicalFormLines.Add(line);
			}

			// Variable constraints display
			StringBuilder varConstraints = new StringBuilder();
			for (int i = 0; i < lp.SignRestrictions.Length; i++)
			{
				string varType = lp.SignRestrictions[i].ToLower();
				string varName = $"x{i + 1}";
				switch (varType)
				{
					case "+": varConstraints.Append($"{varName} ≥ 0; "); break;
					case "-": varConstraints.Append($"{varName} ≤ 0; "); break;
					case "urs": varConstraints.Append($"{varName} unrestricted; "); break;
					case "int": varConstraints.Append($"{varName} E Z; "); break;
					case "bin": varConstraints.Append($"{varName} E {{0,1}}; "); break;
					default: varConstraints.Append($"{varName} unknown; "); break;
				}
			}
			Program.ConicalFormLines.Add("");
			Program.ConicalFormLines.Add("Variable Constraints:");
			Program.ConicalFormLines.Add(varConstraints.ToString().TrimEnd(' ', ';'));

			// =====================
			// Formatted Canonical Form (Table)
			// =====================
			Program.FormattedCanonicalFormLines.Clear();

			// Determine extra variables: slack or artificial
			List<string> extraVarNames = new List<string>();
			for (int i = 0; i < lp.Constraints.Count; i++)
			{
				extraVarNames.Add(lp.Relations[i] == ">=" ? $"e{i + 1}" : $"s{i + 1}");
			}

			// Header row
			StringBuilder header = new StringBuilder("      ");
			for (int i = 0; i < lp.ObjectiveCoefficients.Length; i++)
				header.Append($"{"x" + (i + 1),6}");
			foreach (var ev in extraVarNames)
				header.Append($"{ev,6}");
			header.Append($"{"RHS",6}");
			Program.FormattedCanonicalFormLines.Add(header.ToString());

			// Objective row
			StringBuilder zRow = new StringBuilder("  z  ");
			foreach (var coef in lp.ObjectiveCoefficients)
				zRow.Append($"{-coef,6}"); // negate for tableau
			foreach (var ev in extraVarNames)
				zRow.Append($"{0,6}");
			zRow.Append($"{0,6}");
			Program.FormattedCanonicalFormLines.Add(zRow.ToString());

			// Constraint rows
			for (int i = 0; i < lp.Constraints.Count; i++)
			{
				StringBuilder row = new StringBuilder($" C{i + 1} ");

				double[] rowCoeffs = (double[])lp.Constraints[i].Clone();
				double rhs = lp.RHS[i];
				string rel = lp.Relations[i];

				// Negate >= constraints
				if (rel == ">=")
				{
					for (int j = 0; j < rowCoeffs.Length; j++)
						rowCoeffs[j] *= -1;
					rhs *= -1;
				}

				// Objective variable coefficients
				foreach (var coef in rowCoeffs)
					row.Append($"{coef,6}");

				// Extra variables (1 for corresponding slack/artificial, 0 otherwise)
				for (int j = 0; j < extraVarNames.Count; j++)
					row.Append(i == j ? $"{1,6}" : $"{0,6}");

				row.Append($"{rhs,6}");
				Program.FormattedCanonicalFormLines.Add(row.ToString());
			}

			// Variable constraints (formatted)
			Program.FormattedCanonicalFormLines.Add("");
			Program.FormattedCanonicalFormLines.Add("Var Constraints:");
			Program.FormattedCanonicalFormLines.Add(string.Join(" ",
				lp.SignRestrictions.Select((t, i) => $"x{i + 1}:{t}")));
		}
	}
}
