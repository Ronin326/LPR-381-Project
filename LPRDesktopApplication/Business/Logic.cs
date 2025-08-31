using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace LPRDesktopApplication.Business
{
	internal class Logic
	{
		public static string ReturnCononicalForm()
		{
			foreach (string line in Program.ConicalFormLines)
			{
				Console.WriteLine(line);
			}
			foreach (string line in Program.FormattedCanonicalFormLines)
			{
				Console.WriteLine(line);
			}
			string text = string.Join(Environment.NewLine, Program.ConicalFormLines);

			return text;
		}
		// Function to formate iterations for listview
		public static void PrimalLoadIterationsToListView(System.Windows.Forms.ListView listView, List<string> iterations)
		{
			// Set ListView properties
			listView.View = View.Details;
			listView.FullRowSelect = true;
			listView.GridLines = true;
			listView.HeaderStyle = ColumnHeaderStyle.None; // hide headers
			listView.Columns.Clear();
			listView.Items.Clear();

			// Determine max number of columns
			int maxColumns = 0;
			foreach (string iteration in iterations)
			{
				string[] lines = iteration.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string line in lines)
				{
					string[] cells = line.Split('\t');
					if (cells.Length > maxColumns)
						maxColumns = cells.Length;
				}
			}

			// Add placeholder columns
			for (int i = 0; i < maxColumns-1; i++)
				listView.Columns.Add("", 80);

			// Load iterations
			foreach (string iteration in iterations)
			{
				string[] lines = iteration.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string line in lines)
				{
					string[] cells = line.Split('\t');

					// Iteration heading starting with T-
					if (line.StartsWith("T-"))
					{
						string[] headerWords = line.Split(' '); // split by space
						ListViewItem headerItem = new ListViewItem(headerWords[0]); // T-i

						// Add remaining words to subitems
						for (int i = 1; i < headerWords.Length; i++)
							headerItem.SubItems.Add(headerWords[i]);

						// Fill the rest of the columns with empty subitems
						for (int i = headerWords.Length; i < maxColumns; i++)
							headerItem.SubItems.Add("");

						headerItem.ForeColor = System.Drawing.Color.Orange;
						headerItem.Font = new System.Drawing.Font(listView.Font, System.Drawing.FontStyle.Bold);

						listView.Items.Add(headerItem);
					}
					else
					{
						// Normal table rows
						ListViewItem item = new ListViewItem(cells[0]);
						for (int i = 1; i < maxColumns; i++)
						{
							if (i < cells.Length)
								item.SubItems.Add(cells[i]);
							else
								item.SubItems.Add("");
						}
						listView.Items.Add(item);
					}
				}

				// Add empty row as separator
				ListViewItem empty = new ListViewItem("");
				for (int i = 1; i < maxColumns; i++)
					empty.SubItems.Add("");
				listView.Items.Add(empty);
			}
		}
		// listview fromating for branch and bound
		public static void BranchAndBoundLoadIterationsToListView(System.Windows.Forms.ListView listView, List<string> iterations)
		{
			// Set ListView properties
			listView.View = View.Details;
			listView.FullRowSelect = true;
			listView.GridLines = true;
			listView.HeaderStyle = ColumnHeaderStyle.None; // hide headers
			listView.Columns.Clear();
			listView.Items.Clear();

			// Only one column
			listView.Columns.Add("", 700); // adjust width as needed

			// Load iterations
			foreach (string line in iterations)
			{
				ListViewItem item = new ListViewItem(line);

				// Highlight Root: Z lines in orange
				if (line.StartsWith("Root: Z") || line.StartsWith("Iteration"))
				{
					item.ForeColor = Color.Orange;
					item.Font = new Font(listView.Font, FontStyle.Bold);
				}

				listView.Items.Add(item);
			}
		}
		//Knpsack listview formating
		// Knapsack listview formatting
		// Knapsack listview formatting with auto-size for Solution
		public static void KnapsackFormating(System.Windows.Forms.ListView listView, List<string> iterations)
		{
			// Set ListView properties
			listView.View = View.Details;
			listView.FullRowSelect = true;
			listView.GridLines = true;
			listView.HeaderStyle = ColumnHeaderStyle.None; // hide headers
			listView.Columns.Clear();
			listView.Items.Clear();

			// Determine max number of columns
			int maxColumns = 0;
			foreach (string line in iterations)
			{
				string[] cells = line.Split(new[] { " | " }, StringSplitOptions.None);
				if (cells.Length > maxColumns)
					maxColumns = cells.Length;
			}

			// Add placeholder columns
			for (int i = 0; i < maxColumns; i++)
				listView.Columns.Add("", 120);

			// Load iterations
			foreach (string line in iterations)
			{
				string[] cells = line.Split(new[] { " | " }, StringSplitOptions.None);
				ListViewItem item = new ListViewItem(cells[0]);

				// Add remaining columns
				for (int i = 1; i < maxColumns; i++)
				{
					if (i < cells.Length)
						item.SubItems.Add(cells[i]);
					else
						item.SubItems.Add("");
				}

				// Set colors and font styles
				if (line.StartsWith("Ranking by") || line.StartsWith("Optimal"))
				{
					item.ForeColor = System.Drawing.Color.Orange;
					item.Font = new System.Drawing.Font(listView.Font, FontStyle.Bold);
				}
				else if (line.StartsWith("Max value") || line.StartsWith("Iterations"))
				{
					item.ForeColor = System.Drawing.Color.Cyan;
					item.Font = new System.Drawing.Font(listView.Font, FontStyle.Bold);
				}

				listView.Items.Add(item);
			}

			// Auto-resize the last column if it contains Solution[]
			listView.AutoResizeColumn(maxColumns - 1, ColumnHeaderAutoResizeStyle.ColumnContent);
		}
	}
}
