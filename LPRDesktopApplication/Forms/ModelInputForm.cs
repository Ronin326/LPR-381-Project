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
			//ModelViewTextBox.Text = Business.Logic.ReturnCononicalForm();
		}

		private void button6_Click(object sender, EventArgs e)
		{
			Data.DataHandler.OpenModel();

			//Set textBox text to model
			//ModelViewTextBox.Text = Business.Logic.ReturnCononicalForm();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			var createModelForm = new CreateModelForm();
			createModelForm.ShowDialog();
		}
	}
}
