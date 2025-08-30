using LPRDesktopApplication.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPRDesktopApplication
{
	internal static class Program
	{

		//Global List of strings for cononical form
		public static List<string> ConicalFormLines = new List<string>();

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]

		static void Main()
		{
			ConicalForm.GenerateConicalForm("C:/Users/reina/Desktop/Universiteit/LPR381/LPR-381-Project/LPRDesktopApplication/Input/lp.txt");
            Console.WriteLine("Cononical form:");
            Application.EnableVisualStyles(); 
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
        }
    }
}
