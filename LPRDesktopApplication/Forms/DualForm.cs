using LPR_381_Project.Utils;
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
    public partial class DualForm : Form
    {
        public DualForm()
        {
            InitializeComponent();

            this.Text = "Dual (Canonical Form)";
            this.Width = 800;
            this.Height = 600;

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Consolas", 10)
            };

            var sb = new System.Text.StringBuilder();

            if (DualBuilder.LastWarnings.Count > 0)
            {
                sb.AppendLine("# Warnings:");
                foreach (var w in DualBuilder.LastWarnings)
                    sb.AppendLine("# " + w);
                sb.AppendLine();
            }

            var dualText = string.Join(Environment.NewLine, DualBuilder.DualCanonicalFormLines);
            if (string.IsNullOrWhiteSpace(dualText))
                dualText = "(Dual is empty or failed to parse.)";

            sb.AppendLine(dualText);

            textBox.Text = sb.ToString();
            this.Controls.Add(textBox);
        }

        private void DualForm_Load(object sender, EventArgs e)
        {

        }
    }
}
