using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace LPRDesktopApplication.Forms
{
	public partial class SolutionForm : Form
	{
		public SolutionForm()
		{
			InitializeComponent();
		}

		private void label9_Click(object sender, EventArgs e)
		{

		}

		private void label8_Click(object sender, EventArgs e)
		{

		}
		private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
		{

		}

		private void pictureBox5_Click(object sender, EventArgs e)
		{

		}

		private void SolveModelButton_Click(object sender, EventArgs e)
		{
			ModelInputForm form = new ModelInputForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void HomeButton_Click(object sender, EventArgs e)
		{
			MainForm form = new MainForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void ResultsButton_Click(object sender, EventArgs e)
		{

		}

		private void AnalysisButton_Click(object sender, EventArgs e)
		{
			SensitivityForm form = new SensitivityForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void SolutionForm_Load(object sender, EventArgs e)
		{
			ResultsButton.ForeColor = Color.Orange;
			OptimalZValueLabel.Text = "z = " + Program.OptimalZValue;
			string OptimalXText = "";
			int count = 0;
			foreach(KeyValuePair<string,string> xvar in Program.OptimalVars)
			{
				OptimalXText += xvar.Key + " = " + xvar.Value + "     ";
				count++;
				if (count%4 == 0)
				{
					OptimalXText += "\n";
				}
			}
			OptimalXValuesLabel.Text = OptimalXText;
			Console.WriteLine("Iterations:");
			foreach(string line in Program.Iterations)
			{
				Console.WriteLine(line);
			}
			if (Program.AlgorithmSelected == Algorithms.PrimalSimplex || Program.AlgorithmSelected == Algorithms.DualSimplex)
			{
				Business.Logic.PrimalLoadIterationsToListView(IterationListView, Program.Iterations);
			}
			if (Program.AlgorithmSelected == Algorithms.BanchAndBound || Program.AlgorithmSelected == Algorithms.CuttingPlane)
			{
				Business.Logic.BranchAndBoundLoadIterationsToListView(IterationListView, Program.Iterations);
			}
		}
	}
}
