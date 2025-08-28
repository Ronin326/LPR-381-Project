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
	}
}
