using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LPRDesktopApplication.Models;

namespace LPRDesktopApplication
{
	internal static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{

            string inputPath = @"C:\Users\reina\Desktop\LPR-381-Project\LPRDesktopApplication\Input\lp.txt";
            string outputPath = @"C:\Users\reina\Desktop\LPR-381-Project\LPRDesktopApplication\Output\canonicalLP.txt";
            var cuttingPlaneAlgorithm = new ConicalForm();

            //cuttingPlaneAlgorithm.GenerateConicalForm(inputPath, outputPath);

			// Adjust the file paths for your system


			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
        }
	}
}
