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
	}
}
