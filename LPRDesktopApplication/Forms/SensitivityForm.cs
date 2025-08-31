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
	public partial class SensitivityForm : Form
	{
		public SensitivityForm()
		{
			InitializeComponent();
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
			SolutionForm form = new SolutionForm();
			this.Hide();
			form.ShowDialog();
			this.Close();
		}

		private void SensitivityForm_Load(object sender, EventArgs e)
		{
			AnalysisButton.ForeColor = Color.Orange;
			Models.Sensitivity.SolveAndStoreRanges();
			foreach (var kv in Program.BVars)
				Console.WriteLine($"Basic var {kv.Key}: {kv.Value}");

			foreach (var kv in Program.NBVars)
				Console.WriteLine($"Non-basic var {kv.Key}: {kv.Value}");

			foreach (var kv in Program.RHSValues)
				Console.WriteLine($"RHS {kv.Key}: {kv.Value}");
			//Upate comboboxes
			comboBox1.Items.Clear();
			comboBox1.Items.AddRange(Program.NBVars.Keys.ToArray());

			comboBox2.Items.Clear();
			comboBox2.Items.AddRange(Program.BVars.Keys.ToArray());

			comboBox3.Items.Clear();
			comboBox3.Items.AddRange(Program.RHSValues.Keys.ToArray());

		}

		private void comboBox1_SelectedIndexChanged_1(object sender, EventArgs e)
		{
			// Get the selected key
			string selectedKey = comboBox1.SelectedItem?.ToString();
			if (!string.IsNullOrEmpty(selectedKey) && Program.NBVars.ContainsKey(selectedKey))
			{
				// Set the label text to the value
				label10.Text = Program.NBVars[selectedKey];
			}

		}

		private void comboBox2_SelectedIndexChanged_1(object sender, EventArgs e)
		{
			string selectedKey = comboBox2.SelectedItem?.ToString();
			if (!string.IsNullOrEmpty(selectedKey) && Program.BVars.ContainsKey(selectedKey))
			{
				label11.Text = Program.BVars[selectedKey];
			}
		}

		private void comboBox3_SelectedIndexChanged_1(object sender, EventArgs e)
		{
			string selectedKey = comboBox3.SelectedItem?.ToString();
			if (!string.IsNullOrEmpty(selectedKey) && Program.RHSValues.ContainsKey(selectedKey))
			{
				label15.Text = Program.RHSValues[selectedKey];
			}
		}
	}
}
