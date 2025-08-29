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

		//Global List of strings for cononical form
		public static List<string> ConicalFormLines = new List<string>();

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]

		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
        }
	}
}
