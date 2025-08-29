using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			string text = string.Join(Environment.NewLine, Program.ConicalFormLines);

			return text;
		}
	}
}
