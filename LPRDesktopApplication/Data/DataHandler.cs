using LPRDesktopApplication.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPRDesktopApplication.Data
{
	internal class DataHandler
	{
		public static string ModelPath { get; set; }
		public static void OpenModel()
		{
			// Create the dialog
			OpenFileDialog openFileDialog = new OpenFileDialog();

			// Filter for text files only
			openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
			openFileDialog.Title = "Select a text file";

			// Show dialog
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				// Get the file path
				ModelPath = openFileDialog.FileName;

				// Example: show it in a MessageBox
				MessageBox.Show("Selected file path: " + ModelPath);
			}

			//Get Cononical form
			Models.ConicalForm conical = new Models.ConicalForm();

			conical.GenerateConicalForm(ModelPath);
			
		}
	}
}
