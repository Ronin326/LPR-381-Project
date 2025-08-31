using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LPR_381_Project.Utils;
using System.Windows.Forms;

namespace LPRDesktopApplication
{
    public static class DualRunner
    {
        /// <summary>
        /// Build the dual, copy it into Program.FormattedCanonicalFormLines, optionally set algorithm to DualSimplex.
        /// Returns false if dual failed to build.
        /// </summary>
        public static bool PrepareDualForSolving(bool setAlgorithmToDualSimplex = true)
        {
            DualBuilder.BuildAndStoreDualFromCurrent();

            if (DualBuilder.DualCanonicalFormLines == null || DualBuilder.DualCanonicalFormLines.Count == 0)
            {
                MessageBox.Show("(Dual is empty or failed to parse.)\r\n\r\n" +
                                string.Join(Environment.NewLine, DualBuilder.LastWarnings ?? new List<string>()),
                                "Dual Build", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Put the dual lines into the same global list your pipeline already uses
            Program.FormattedCanonicalFormLines = new List<string>(DualBuilder.DualCanonicalFormLines); // uses your Program global
            if (setAlgorithmToDualSimplex)
            {
                Program.AlgorithmSelected = Algorithms.DualSimplex; // your enum has DualSimplex
            }

            return true;
        }


        public static string SaveDualToOutput(string fileName = "Dual.txt")
        {
            var lines = DualBuilder.DualCanonicalFormLines ?? new List<string> { "(no dual)" };

            // Try project root ..\..\Output from bin\Debug/Release
            string tryRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Output"));
            string altBin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
            string outDir = tryRoot;

            try
            {
                Directory.CreateDirectory(outDir);
            }
            catch
            {
                outDir = altBin;
                Directory.CreateDirectory(outDir);
            }

            string full = Path.Combine(outDir, fileName);
            File.WriteAllLines(full, lines);
            return full;
        }
    }
}
