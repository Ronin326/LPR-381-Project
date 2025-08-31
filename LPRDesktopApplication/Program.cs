using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LPRDesktopApplication.Models;

namespace LPRDesktopApplication
{
	//Enum for algorithms
	public enum Algorithms
	{
		PrimalSimplex = 1,
		DualSimplex,
		BanchAndBound,
		CuttingPlane,
		Knapsack
	}
	internal static class Program
	{

		//Global List of strings for cononical form
		public static List<string> ConicalFormLines = new List<string>();
		public static List<string> FormattedCanonicalFormLines = new List<string>();
		public static List<string> RelaxedOptimalFormattedLines = new List<string>();

		//Global Var for solution
		public static string OptimalZValue = ""; 
		public static Dictionary<string, string> OptimalVars = new Dictionary<string, string>();
		public static List<string> Iterations = new List<string>();

		//Global Vars vor Sensitivy
		public static Dictionary<string, string> NBVars = new Dictionary<string,string>();
		public static Dictionary<string, string> BVars = new Dictionary<string, string>();
		public static Dictionary<string, string> RHSValues = new Dictionary<string, string>();

		//Global var for algoritm
		public static Algorithms AlgorithmSelected = new Algorithms();

		//Vars for knapsack
		public static List<int> KnapsackValue = new List<int>();
		public static List<int> KnapsackWeight = new List<int>();
		public static double KnapsackMaxWeight = 0;

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
