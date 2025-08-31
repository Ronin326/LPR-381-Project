using LPRDesktopApplication.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPRDesktopApplication.Forms
{
	public partial class ModelInputForm : Form
	{
		public ModelInputForm()
		{
			InitializeComponent();
		}

		private void HomeButton_Click(object sender, EventArgs e)
		{
			MainForm form = new MainForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void label4_Click(object sender, EventArgs e)
		{

		}

		private void label3_Click(object sender, EventArgs e)
		{

		}

		private void radioButton1_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void radioButton2_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void radioButton3_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void radioButton4_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void radioButton5_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void ResultsButton_Click(object sender, EventArgs e)
		{
			SolutionForm form = new SolutionForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void AnalysisButton_Click(object sender, EventArgs e)
		{
			SensitivityForm form = new SensitivityForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			Data.DataHandler.OpenModel();

			//Set textBox text to model
			ModelViewTextBox.Text = Business.Logic.ReturnCononicalForm();
		}

		private void button6_Click(object sender, EventArgs e)
		{
			Data.DataHandler.OpenModel();

			//Set textBox text to model
			ModelViewTextBox.Text = Business.Logic.ReturnCononicalForm();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			var createModelForm = new CreateModelForm();
			createModelForm.ShowDialog();
		}

		private void PrimalSideButton_Click(object sender, EventArgs e)
		{
			PrimalRadioButton.Checked = true;
		}

		private void DualSideButton_Click(object sender, EventArgs e)
		{
			DualRadioButton.Checked = true;
		}

		private void BranchSideButton_Click(object sender, EventArgs e)
		{
			BranchRadioButton.Checked = true;
		}

		private void CuttingSideButton_Click(object sender, EventArgs e)
		{
			CuttinRadioButton.Checked = true;
		}

		private void KnapsackSideButton_Click(object sender, EventArgs e)
		{
			KnapsackRadioButton.Checked = true;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if (Program.ConicalFormLines.Count > 0)
			{
				if (PrimalRadioButton.Checked)
				{
					Console.WriteLine("Primal Simplex Selected");
					Program.AlgorithmSelected = Algorithms.PrimalSimplex;
					Models.PrimalSimplex.SolveFromFormattedCanonical();
					Console.WriteLine("Optimal Z Value:",Program.OptimalZValue);
					foreach (string line in Program.RelaxedOptimalFormattedLines)
					{
						Console.WriteLine(line);
					}
					SolutionForm form = new SolutionForm();
					this.Hide();
					form.ShowDialog();
					this.Close();
				}
				else if (DualRadioButton.Checked)
				{
					Console.WriteLine("Dual Simplex Selected");
					Program.AlgorithmSelected = Algorithms.DualSimplex;
					Models.DualSimplex.SolveFromFormattedCanonical();
					Console.WriteLine("Optimal Z Value:", Program.OptimalZValue);
					SolutionForm form = new SolutionForm();
					this.Hide();
					form.ShowDialog();
					this.Close();
				}
				else if (BranchRadioButton.Checked)
				{
					Console.WriteLine("Branch and Bound Selected");
					Program.AlgorithmSelected = Algorithms.BanchAndBound;
					Models.RelaxedModel.SolveFromFormattedCanonical();
					foreach (string line in Program.RelaxedOptimalFormattedLines)
					{
						Console.WriteLine(line);
					}
					Console.WriteLine("Optimal Z Value: " + Program.OptimalZValue);
					try
					{
						BranchAndBound.Solve(); // No parameter needed now
					}
					catch (Exception ex)
					{
						MessageBox.Show("Error: " + ex.Message);
					}
					Console.WriteLine("Optimal Z Value: " + Program.OptimalZValue);
					SolutionForm form = new SolutionForm();
					this.Hide();
					form.ShowDialog();
					this.Close();
				}
				else if (CuttinRadioButton.Checked)
				{
					Console.WriteLine("Cutting Plane Selected");
					Program.AlgorithmSelected = Algorithms.CuttingPlane;

					// Make sure the relaxed model is solved first
					Models.RelaxedModel.SolveFromFormattedCanonical();

					// Print the relaxed solution
					foreach (string line in Program.RelaxedOptimalFormattedLines)
						Console.WriteLine(line);

					try
					{
						var result = CuttingPlane.Solve(); // Call Cutting Plane
						foreach (string line in result.IterationLogs)
							Console.WriteLine(line);

						Console.WriteLine("Optimal Z Value: " + result.OptimalValue);
						if (result.OptimalSolution != null)
							Console.WriteLine("Optimal Solution: [" + string.Join(", ", result.OptimalSolution.Select(x => x.ToString("0.##"))) + "]");
					}
					catch (Exception ex)
					{
						MessageBox.Show("Error: " + ex.Message);
					}

					SolutionForm form = new SolutionForm();
					this.Hide();
					form.ShowDialog();
					this.Close();
				}
				else if (KnapsackRadioButton.Checked)
				{
					Console.WriteLine("Knapsack Selected");
					Program.AlgorithmSelected = Algorithms.Knapsack;
					if (Program.KnapsackValue.Count > 0)
					{
						Models.Knapsack.Solve();
						SolutionForm form = new SolutionForm();
						this.Hide();
						form.ShowDialog();
						this.Close();
					}
					else
					{
						MessageBox.Show("Model cant be solved using Knapsack");
					}
				}
				else
				{
					MessageBox.Show("Please Select a Algorithm");
				}
			}
			else
			{
				MessageBox.Show("Please Load a Model to Solve");
			}
		}

		private void ModelInputForm_Load(object sender, EventArgs e)
		{
			SolveModelButton.ForeColor = Color.Orange;
		}
	}
}
