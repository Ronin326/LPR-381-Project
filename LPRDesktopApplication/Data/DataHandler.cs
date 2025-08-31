using LPRDesktopApplication.Forms;
using LPRDesktopApplication.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPRDesktopApplication.Data
{
	internal class DataHandler
	{
		public static string ModelPath { get; set; }
		public static void OpenModel()
		{
			// Create the dialog
			OpenFileDialog openFileDialog = new OpenFileDialog();

			// Filter for text files only
			openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
			openFileDialog.Title = "Select a text file";

			// Show dialog
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				// Get the file path
				ModelPath = openFileDialog.FileName;

				// Example: show it in a MessageBox
				MessageBox.Show("Selected file path: " + ModelPath);
			}

			//Get Cononical form
			ConicalForm.GenerateConicalForm(ModelPath);
			KnapsackFormatText(ModelPath);
			
		}
		//Knapsack Parser
		public static void KnapsackFormatText(string path)
		{

			var lines = File.ReadAllLines(path)
				.Where(l => !string.IsNullOrWhiteSpace(l))
				.Select(l => l.Trim())
				.ToArray();

			if (lines.Length < 3)
				throw new InvalidOperationException("Expected: objective line, >=1 constraint line, and a sign-restrictions line.");

			if (lines[lines.Length - 1].Contains("bin"))
			{
				// Values (objective coefficients)
				Program.KnapsackValue = lines[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
										.Skip(1)
										.Select(s => int.Parse(s.Replace("+", "")))
										.ToList();

				// Weights (RHS of constraints)
				var weightParts = lines[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				Program.KnapsackWeight = weightParts.Take(weightParts.Length - 2)
											.Select(s => int.Parse(s.Replace("+", "")))
											.ToList();

				// Max capacity
				Program.KnapsackMaxWeight = double.Parse(weightParts.Last(), CultureInfo.InvariantCulture);
			}
		}
		//Export to output Folder
		public static void ExportResults()
		{
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.Filter = "Text File|*.txt";
				saveFileDialog.Title = "Save Knapsack Results";
				saveFileDialog.FileName = "Exported Text.txt";

				if (saveFileDialog.ShowDialog() == DialogResult.OK)
				{
					try
					{
						List<string> text = new List<string>();

						text.Add("Optimal Z Value:\nz = " + Program.OptimalZValue);
						text.Add("\nOptimal X values:");
						foreach (KeyValuePair<string, string> var in Program.OptimalVars)
						{
							text.Add(var.Key + ": " + var.Value);
						}

						text.Add("\nIterations:");
						foreach (string line in Program.Iterations)
						{
							text.Add(line);
						}
						File.WriteAllLines(saveFileDialog.FileName, text);
						MessageBox.Show("Results exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
					catch (IOException ex)
					{
						MessageBox.Show($"Error saving file: {ex.Message}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
			}
		}
	}
}
