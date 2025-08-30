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
        public static void GenerateConicalForm(string inputFilePath)
        {
            try
            {
                // Read all lines from the input file
                string[] lines = File.ReadAllLines(inputFilePath);
                if (lines.Length < 2)
                    throw new ArgumentException("Input file must have at least 2 lines (objective and at least one constraint).");

                // Parse the objective (max/min and coefficients)
                string maxOrMin = lines[0].Trim().ToLower();
                if (maxOrMin != "max" && maxOrMin != "min")
                    throw new ArgumentException("First line must be 'max' or 'min'.");

                // Parse objective coefficients
                string[] objTokens = lines[1].Trim().Split(' ');
                List<double> objCoefficients = new List<double>();
                foreach (string token in objTokens)
                {
                    if (!double.TryParse(token, out double coef))
                        throw new ArgumentException($"Invalid objective coefficient: {token}");
                    objCoefficients.Add(coef);
                }
                int numVariables = objCoefficients.Count;

                // Parse constraints
                List<List<double>> constraintCoefficients = new List<List<double>>();
                List<string> constraintSigns = new List<string>();
                List<double> rhsValues = new List<double>();
                int slackCount = 0, surplusCount = 0;

                for (int i = 2; i < lines.Length - 1; i++)
                {
                    string[] tokens = lines[i].Trim().Split(' ');
                    if (tokens.Length < numVariables + 2)
                        throw new ArgumentException($"Constraint {i - 1} has insufficient tokens.");

                    // Parse coefficients
                    List<double> coeffs = new List<double>();
                    for (int j = 0; j < numVariables; j++)
                    {
                        if (!double.TryParse(tokens[j], out double coef))
                            throw new ArgumentException($"Invalid coefficient in constraint {i - 1}: {tokens[j]}");
                        coeffs.Add(coef);
                    }
                    constraintCoefficients.Add(coeffs);

                    // Parse sign
                    string sign = tokens[numVariables];
                    if (sign != "<=" && sign != ">=" && sign != "=")
                        throw new ArgumentException($"Invalid constraint sign in constraint {i - 1}: {sign}");
                    constraintSigns.Add(sign);
                    if (sign == "<=") slackCount++;
                    else if (sign == ">=") surplusCount++;

                    // Parse RHS
                    if (!double.TryParse(tokens[numVariables + 1], out double rhs))
                        throw new ArgumentException($"Invalid RHS in constraint {i - 1}: {tokens[numVariables + 1]}");
                    rhsValues.Add(rhs);
                }

                // Parse variable types
                string[] varTypes = lines[lines.Length - 1].Trim().Split(' ');
                if (varTypes.Length != numVariables)
                    throw new ArgumentException($"Last line must have exactly {numVariables} type specifiers (one for each variable).");
                List<string> variableConstraints = varTypes.ToList();
                foreach (string constraint in variableConstraints)
                {
                    if (constraint != "+" && constraint != "-" && constraint != "int" && constraint != "bin")
                        throw new ArgumentException($"Invalid variable constraint: {constraint}. Must be '+', '-', 'int', or 'bin'.");
                }

                // Prepare output
                string outputFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath),
                    Path.GetFileNameWithoutExtension(inputFilePath) + "_formatted.txt");
                using (StreamWriter writer = new StreamWriter(outputFilePath))
                {
                    // Write header
                    List<string> header = new List<string> { maxOrMin };
                    for (int i = 1; i <= numVariables; i++)
                        header.Add($"x{i}");
                    for (int i = 1; i <= slackCount; i++)
                        header.Add($"s{i}");
                    for (int i = 1; i <= surplusCount; i++)
                        header.Add($"e{i}");
                    header.Add("rhs");
                    writer.WriteLine(string.Join("\t", header));

                    // Write objective row
                    List<string> objRow = new List<string> { "z" };
                    objRow.AddRange(objCoefficients.Select(c => c.ToString()));
                    objRow.AddRange(Enumerable.Repeat("0", slackCount + surplusCount));
                    objRow.Add("0");
                    writer.WriteLine(string.Join("\t", objRow));

                    // Write constraint rows
                    int currentSlack = 1, currentSurplus = 1;
                    for (int i = 0; i < constraintCoefficients.Count; i++)
                    {
                        List<string> row = new List<string> { (i + 1).ToString() };
                        row.AddRange(constraintCoefficients[i].Select(c => c.ToString()));
                        // Add slack/surplus variables
                        List<string> slackSurplus = Enumerable.Repeat("0", slackCount + surplusCount).ToList();
                        if (constraintSigns[i] == "<=")
                        {
                            slackSurplus[currentSlack - 1] = "1";
                            currentSlack++;
                        }
                        else if (constraintSigns[i] == ">=")
                        {
                            slackSurplus[slackCount + currentSurplus - 1] = "-1";
                            currentSurplus++;
                        }
                        row.AddRange(slackSurplus);
                        row.Add(rhsValues[i].ToString());
                        writer.WriteLine(string.Join("\t", row));
                    }

                    // Write sign row
                    List<string> signRow = new List<string> { "sign" };
                    signRow.AddRange(variableConstraints);
                    writer.WriteLine(string.Join("\t", signRow));
                }

                Console.WriteLine($"Output written to {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        
        }
    }
}