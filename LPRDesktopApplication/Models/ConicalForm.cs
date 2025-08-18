using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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

        public void GenerateConicalForm(string path, string output)
        {
            var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToArray();
            
            if (lines.Length < 3)
                throw new InvalidOperationException("Expected: objective line, >=1 constraint line, and a sign-restrictions line.");

            LinearProgram lp = new LinearProgram();
            lp.IsMaximization = lines[0].Contains("max");
            var coeficients = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Where(c => c != "max" && c != "min").Select(c => double.Parse(c, CultureInfo.InvariantCulture)) .ToArray();
            lp.ObjectiveCoefficients = coeficients;

            for (int i = 1; i < lines.Length - 1; i++)
            {
                var parts = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                var coefficients = parts.Take(parts.Length - 2).Select(c => double.Parse(c, CultureInfo.InvariantCulture)).ToArray();
                lp.Constraints.Add(coefficients);

                lp.Relations.Add(parts[parts.Length - 2]);

                lp.RHS.Add(double.Parse(parts.Last(), CultureInfo.InvariantCulture));
            }
            lp.SignRestrictions = lines[lines.Length - 1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToArray();

            //Create Conical Form
            List<string> lpLines = new List<string>();
            lpLines.Clear();
            string objLine = lp.IsMaximization ? "max z = " : "min z = ";

            for (int i = 0; i < lp.ObjectiveCoefficients.Length; i++)
            {
                double coef = lp.ObjectiveCoefficients[i];

                if (i == 0)
                    objLine += $"-{coef.ToString(CultureInfo.InvariantCulture)}x{i + 1}";
                else
                    objLine += $" - {coef.ToString(CultureInfo.InvariantCulture)}x{i + 1}";
            }
            lpLines.Add(objLine);
            int k = 1;
            for (int i = 0; i < lp.Constraints.Count; i++)
            {
                var coeffs = (double[])lp.Constraints[i].Clone();
                double rhs = lp.RHS[i];
                string rel = lp.Relations[i];

                if (rel == ">=")
                {
                    // Flip all coefficients and RHS
                    for (int j = 0; j < coeffs.Length; j++)
                        coeffs[j] *= -1;
                    rhs *= -1;

                    // Now add +e_k
                    string line = "";
                    for (int j = 0; j < coeffs.Length; j++)
                    {
                        if (j == 0)
                            line += $"{coeffs[j]}x{j + 1}";
                        else
                            line += $" {(coeffs[j] >= 0 ? "+" : "-")} {Math.Abs(coeffs[j])}x{j + 1}";
                    }

                    line += $" + e{k} = {rhs}";
                    k++;

                    lpLines.Add(line);
                }else if(rel == "<=")
                {
                    // Keep as is, just add slack variable
                    string line = "";
                    for (int j = 0; j < coeffs.Length; j++)
                    {
                        if (j == 0)
                            line += $"{coeffs[j]}x{j + 1}";
                        else
                            line += $" {(coeffs[j] >= 0 ? "+" : "-")} {Math.Abs(coeffs[j])}x{j + 1}";
                    }
                    line += $" + s{k} = {rhs}";
                    k++;
                    lpLines.Add(line);
                }
              

            }
            int counter = 1;
            string Restriction = "";
            foreach (string sign in lp.SignRestrictions)
            {
                Restriction += $"{sign} x{counter} ";
                counter++;
            }

            lpLines.Add(Restriction);

            File.WriteAllLines(output, lpLines);
        }
    }
}
