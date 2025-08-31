using LPR_381_Project.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace LPRDesktopApplication.Forms
{
	public partial class SensitivityForm : Form
	{
        private Label _shadowLabel;
        private string _shadowText = "(parsing...)";
        private readonly Font _mono = new Font("Consolas", 10f);
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

            // Create the label if not already
            if (_shadowLabel == null)
            {
                _shadowLabel = new Label();
                _shadowLabel.AutoSize = true;
                _shadowLabel.Font = new Font("Consolas", 10f);
                _shadowLabel.ForeColor = Color.White;
                _shadowLabel.BackColor = panel9.BackColor;
                _shadowLabel.TextAlign = ContentAlignment.TopLeft;

      
                _shadowLabel.Location = new Point(5, 30);

                _shadowLabel.MaximumSize = new Size(panel9.Width - 20, 0);

                panel9.AutoScroll = true; 
                panel9.Controls.Add(_shadowLabel);
            }


            _shadowLabel.Text = _shadowText;
        }



        private void button3_Click(object sender, EventArgs e)
        {
            DualBuilder.BuildAndStoreDualFromCurrent();

            // Open in a new window
            var dualForm = new DualForm();
            dualForm.Show(); // Use ShowDialog() if you want it moda

        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (!DualRunner.PrepareDualForSolving(setAlgorithmToDualSimplex: true))
                return;

 
            var where = DualRunner.SaveDualToOutput("Dual.txt");

            DualStandaloneSolver.SolveDualFromFileAndShow("Dual.txt");
            if (DualStandaloneSolver.LastSuccess)
            {
                // Build text for the label
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Shadow Prices:");
                foreach (var kv in DualStandaloneSolver.LastY.OrderBy(k => k.Key))
                {
                    sb.AppendLine($"{kv.Key} = {kv.Value:0.##########}");
                }

                sb.AppendLine();
                sb.AppendLine($"z* (min) = {DualStandaloneSolver.LastZ:0.##########}");

                _shadowText = sb.ToString();
            }
            else
            {
                _shadowText = "Failed to solve dual.\r\n" + DateTime.Now;
            }

            // Ensure label exists
            if (_shadowLabel == null)
            {
                _shadowLabel = new Label();
                _shadowLabel.AutoSize = true;
                _shadowLabel.Font = new Font("Consolas", 10f);
                _shadowLabel.ForeColor = Color.White;
                _shadowLabel.BackColor = panel9.BackColor;
                _shadowLabel.TextAlign = ContentAlignment.TopLeft;
                _shadowLabel.Location = new Point(5, 30);
                _shadowLabel.MaximumSize = new Size(panel9.Width - 20, 0);
                panel9.AutoScroll = true;
                panel9.Controls.Add(_shadowLabel);
            }

            // Update the label text
            _shadowLabel.Text = _shadowText;
        }

        private void button2_Click(object sender, EventArgs e)
        { 

            DualityVerifier.VerifyAndShow(tol: 1e-6);
        }

    }
}
