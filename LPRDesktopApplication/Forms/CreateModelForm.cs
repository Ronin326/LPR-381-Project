using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LPRDesktopApplication.Forms
{
	public class CreateModelForm : Form
	{
		private TextBox txtFileName;
		private TextBox txtObjective;
		private TextBox txtConstraints;
		private TextBox txtVarConstraints;
		private Button btnSave;

		private string FixedPath = Path.Combine(
			Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..")),
			"Input");

		public CreateModelForm()
		{
			// Form properties
			this.Text = "Create LP Model";
			this.Width = 500;
			this.Height = 500;
			this.BackColor = Color.Black;
			this.Font = new Font("Arial", 10);
			this.StartPosition = FormStartPosition.CenterScreen;

			int marginTop = 10;
			int marginLeft = 10;
			int labelHeight = 20;
			int inputHeight = 25;
			int spacing = 5;

			// File Name
			Label lblFileName = new Label()
			{
				Text = "File Name (without extension):",
				Top = marginTop,
				Left = marginLeft,
				Width = 450,
				Height = labelHeight,
				ForeColor = Color.White
			};
			txtFileName = new TextBox()
			{
				Top = lblFileName.Bottom + spacing,
				Left = marginLeft,
				Width = 450,
				Height = inputHeight,
				Font = this.Font,
				BackColor = Color.DimGray,
				ForeColor = Color.White
			};

			// Objective
			Label lblObjective = new Label()
			{
				Text = "Objective Function (keep format, e.g., max +3 +5):",
				Top = txtFileName.Bottom + 15,
				Left = marginLeft,
				Width = 450,
				Height = labelHeight,
				ForeColor = Color.White
			};
			txtObjective = new TextBox()
			{
				Top = lblObjective.Bottom + spacing,
				Left = marginLeft,
				Width = 450,
				Height = inputHeight,
				Font = this.Font,
				BackColor = Color.DimGray,
				ForeColor = Color.White
			};

			// Constraints
			Label lblConstraints = new Label()
			{
				Text = "Constraints (one per line, keep format, e.g., +1 +2 <= 14):",
				Top = txtObjective.Bottom + 15,
				Left = marginLeft,
				Width = 450,
				Height = labelHeight,
				ForeColor = Color.White
			};
			txtConstraints = new TextBox()
			{
				Top = lblConstraints.Bottom + spacing,
				Left = marginLeft,
				Width = 450,
				Height = 150,
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				Font = this.Font,
				BackColor = Color.DimGray,
				ForeColor = Color.White
			};

			// Variable Constraints
			Label lblVarConstraints = new Label()
			{
				Text = "Variable Constraints (keep format, e.g., int int bin urs):",
				Top = txtConstraints.Bottom + 15,
				Left = marginLeft,
				Width = 450,
				Height = labelHeight,
				ForeColor = Color.White
			};
			txtVarConstraints = new TextBox()
			{
				Top = lblVarConstraints.Bottom + spacing,
				Left = marginLeft,
				Width = 450,
				Height = inputHeight,
				Font = this.Font,
				BackColor = Color.DimGray,
				ForeColor = Color.White
			};

			// Save Button
			btnSave = new Button()
			{
				Text = "Save LP Model",
				Top = txtVarConstraints.Bottom + 20,
				Left = marginLeft,
				Width = 150,
				Height = 35,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(0, 189, 164),
				ForeColor = Color.White,
				Font = new Font("Arial", 10, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			btnSave.FlatAppearance.BorderSize = 0;
			btnSave.Click += BtnSave_Click;

			// Add controls
			this.Controls.Add(lblFileName);
			this.Controls.Add(txtFileName);
			this.Controls.Add(lblObjective);
			this.Controls.Add(txtObjective);
			this.Controls.Add(lblConstraints);
			this.Controls.Add(txtConstraints);
			this.Controls.Add(lblVarConstraints);
			this.Controls.Add(txtVarConstraints);
			this.Controls.Add(btnSave);
		}

		private void BtnSave_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrWhiteSpace(txtFileName.Text) ||
				string.IsNullOrWhiteSpace(txtObjective.Text) ||
				string.IsNullOrWhiteSpace(txtConstraints.Text) ||
				string.IsNullOrWhiteSpace(txtVarConstraints.Text))
			{
				MessageBox.Show("Please fill all fields.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			try
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(txtObjective.Text.Trim());

				foreach (var line in txtConstraints.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
				{
					sb.AppendLine(line.Trim());
				}

				sb.AppendLine(txtVarConstraints.Text.Trim());

				string filePath = Path.Combine(FixedPath, txtFileName.Text + ".txt");
				File.WriteAllText(filePath, sb.ToString());
				MessageBox.Show($"LP Model saved successfully to:\n{filePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error saving file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}
