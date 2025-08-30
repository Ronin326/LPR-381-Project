using LPRDesktopApplication;
using LPRDesktopApplication.Models;
using System;
using System.IO;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            string inputDir = @"C:\Users\reina\Desktop\Universiteit\LPR381\LPR-381-Project\LPRDesktopApplication\Input";
            string inputFilePath = Path.Combine(inputDir, "lp.txt");

            // Verify input file exists
            if (!File.Exists(inputFilePath))
            {
                MessageBox.Show($"Input file not found at: {inputFilePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Generate canonical form
            ConicalForm.GenerateConicalForm(inputFilePath);
            string canonicalFilePath = Path.Combine(inputDir, Path.GetFileNameWithoutExtension(inputFilePath) + "_formatted.txt");

            // Verify canonical file was created
            if (!File.Exists(canonicalFilePath))
            {
                MessageBox.Show($"Canonical form file not created at: {canonicalFilePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Solve using Branch and Bound
            BranchAndBoundSolver.Solve(canonicalFilePath);
            string outputFilePath = Path.Combine(inputDir, "BranchAndBound.txt");

            // Verify output file was created
            if (!File.Exists(outputFilePath))
            {
                MessageBox.Show($"Branch and Bound output file not created at: {outputFilePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show($"Processing completed. Files created at:\n{canonicalFilePath}\n{outputFilePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Start the Windows Forms application
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}\nStack Trace: {ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}