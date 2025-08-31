using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            // Existing shadow price parsing
            try
            {
                var lines = ReadExportedTextFile();
                BuildShadowTextFromLines(lines);
            }
            catch (Exception ex)
            {
                _shadowText = "Error: " + ex.Message;
            }

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


        // Read "Output/Exported Text.txt" from the SOLUTION/PROJECT root (not bin\Debug)
        private static string GetSolutionOutputDirectory()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory; // ...\bin\Debug\
                                                                // walk up 2 levels: Debug -> bin -> project root
            var projectRoot = Directory.GetParent(exeDir).Parent.Parent.FullName;
            return Path.Combine(projectRoot, "Output");
        }

        private static string[] ReadExportedTextFile()
        {
            var outputDir = GetSolutionOutputDirectory();
            var path = Path.Combine(outputDir, "Exported Text.txt");

            if (!File.Exists(path))
                throw new FileNotFoundException("Exported Text.txt not found at: " + path);

            return File.ReadAllLines(path);
        }

        // Parse “Optimal X values” block (xk: 0/1)
        private static Dictionary<string, int> ParseOptimalX(string[] lines)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int idx = Array.FindIndex(lines, s => s.Trim().Equals("Optimal X values:", StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return dict;

            for (int i = idx + 1; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (string.IsNullOrEmpty(t)) break;                 // end of block
                if (!t.StartsWith("x", StringComparison.OrdinalIgnoreCase)) break;

                // format: x3: 1
                var parts = t.Split(':');
                if (parts.Length >= 2)
                {
                    var name = parts[0].Trim();                     // e.g., "x3"
                    var valStr = parts[1].Trim();
                    int val;
                    if (int.TryParse(valStr, out val))
                        dict[name] = val;
                }
            }
            return dict;
        }

        // Parse “Ranking by | value/weight | ratio” block (xk | 0,375)
        private static Dictionary<string, double> ParseRatios(string[] lines)
        {
            var ratios = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            int idx = Array.FindIndex(lines, s => s.Trim().StartsWith("Ranking by", StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return ratios;

            for (int i = idx + 1; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (string.IsNullOrEmpty(t)) break;
                if (!t.StartsWith("x", StringComparison.OrdinalIgnoreCase)) break; // next section

                // format like: "x3 | 0,5" (note comma decimal)
                var parts = t.Split('|');
                if (parts.Length >= 2)
                {
                    var name = parts[0].Trim();                         // e.g., "x3"
                    var ratioStr = parts[1].Trim().Replace(',', '.');   // normalize comma to dot
                    double r;
                    if (double.TryParse(ratioStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r))
                    {
                        ratios[name] = r;
                    }
                }
            }

            return ratios;
        }

        // Build the text we’ll draw on the panel: directional shadow prices + interval
        private void BuildShadowTextFromLines(string[] lines)
        {
            var opt = ParseOptimalX(lines);
            var ratios = ParseRatios(lines);

            if (opt.Count == 0 || ratios.Count == 0)
            {
                _shadowText = "Could not find 'Optimal X values' or 'Ranking by | value/weight | ratio' sections.";
                return;
            }

            // Split into included vs excluded (according to Optimal X)
            var included = new List<double>();
            var excluded = new List<double>();

            foreach (var kv in ratios)
            {
                var name = kv.Key;  // "x3"
                var r = kv.Value;
                int chosen;
                if (opt.TryGetValue(name, out chosen) && chosen == 1)
                    included.Add(r);
                else
                    excluded.Add(r);
            }

            if (included.Count == 0 || excluded.Count == 0)
            {
                _shadowText = "Need at least one included and one excluded item to compute directional shadow prices.";
                return;
            }

            // Directional shadow prices for the capacity constraint (LP relaxation)
            // y+ (increase capacity): next gain comes from best excluded ratio
            // y- (decrease capacity): loss comes from worst included ratio
            double yPlus = excluded.Max();
            double yMinus = included.Min();

            // Interval of optimal duals when there is no fractional item
            double lo = yPlus;
            double hi = yMinus;

            // Pretty print
            var sb = new StringBuilder();
            sb.AppendLine("=== SHADOW PRICES (Knapsack LP Relaxation) ===");
            sb.AppendLine("Directional marginals for capacity W:");
            sb.AppendLine($"  Increase W by +Δ → dZ/dW ≈ {yPlus:0.######}");
            sb.AppendLine($"  Decrease W by -Δ → dZ/dW ≈ {yMinus:0.######}");
            sb.AppendLine();
            sb.AppendLine("Optimal dual interval (no fractional item case):");
            sb.AppendLine($"  y ∈ [{lo:0.######}, {hi:0.######}]");
            sb.AppendLine();
            sb.AppendLine("Notes:");
            sb.AppendLine(" • y+ uses the highest ratio among excluded items.");
            sb.AppendLine(" • y- uses the lowest ratio among included items.");
            sb.AppendLine(" • Interval appears when the greedy fill hits capacity exactly (no fractional item).");

            _shadowText = sb.ToString();
        }

        private void panel9_Paint(object sender, PaintEventArgs e)
        {
        }
    }
}
