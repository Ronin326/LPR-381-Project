using System;
using System.Collections.Generic;
using System.Linq;

namespace LPRDesktopApplication.Models
{
	internal class Knapsack
	{
		public static void Solve()
		{
			int n = Program.KnapsackValue.Count;

			if (n == 0 || Program.KnapsackWeight.Count != n || Program.KnapsackMaxWeight <= 0)
			{
				Program.Iterations.Add("Knapsack data not loaded properly.");
				Program.OptimalZValue = "0";
				return;
			}

			// Ranking items by value/weight ratio
			var ratios = new List<(int index, double ratio)>();
			for (int i = 0; i < n; i++)
				ratios.Add((i, (double)Program.KnapsackValue[i] / Program.KnapsackWeight[i]));

			var ranked = ratios.OrderByDescending(r => r.ratio).ToList();

			Program.Iterations.Add("Ranking by | value/weight | ratio");
			foreach (var r in ranked)
				Program.Iterations.Add($"x{r.index + 1} | {r.ratio:0.######}");
			Program.Iterations.Add(""); // blank line

			// Branch & bound stack
			int bestValue = 0;
			int[] bestSolution = new int[n];
			var stack = new Stack<(int idx, int currentValue, int currentWeight, int[] currentSolution, string subProblem)>();
			stack.Push((0, 0, 0, new int[n], "0"));

			int maxIterations = 1000;
			int iterationCount = 0;

			while (stack.Count > 0 && iterationCount < maxIterations)
			{
				iterationCount++;
				var (idx, currentValue, currentWeight, currentSolution, subProblem) = stack.Pop();

				// Log current attempt cleanly
				Program.Iterations.Add($"Sub Prob | {subProblem.PadRight(5)} | Value={currentValue,3} | Weight={currentWeight,3} | Solution=[{string.Join(",", currentSolution)}]");

				if (idx == n)
				{
					if (currentValue > bestValue)
					{
						bestValue = currentValue;
						bestSolution = (int[])currentSolution.Clone();
					}
					continue;
				}

				int itemIndex = ranked[idx].index;

				// Include item if it fits
				if (currentWeight + Program.KnapsackWeight[itemIndex] <= Program.KnapsackMaxWeight)
				{
					var includeSolution = (int[])currentSolution.Clone();
					includeSolution[itemIndex] = 1;
					stack.Push((idx + 1,
						currentValue + Program.KnapsackValue[itemIndex],
						currentWeight + Program.KnapsackWeight[itemIndex],
						includeSolution,
						subProblem + ".1")); // clean numbering for include
				}

				// Exclude item
				stack.Push((idx + 1,
					currentValue,
					currentWeight,
					currentSolution,
					subProblem + ".2")); // clean numbering for exclude
			}

			// Store results in global program vars
			Program.OptimalZValue = bestValue.ToString();
			Program.OptimalVars.Clear();
			for (int i = 0; i < n; i++)
				Program.OptimalVars[$"x{i + 1}"] = bestSolution[i].ToString();

			// Log final optimal solution
			Program.Iterations.Add("");
			Program.Iterations.Add("Optimal | Knapsack | Solution");
			for (int i = 0; i < n; i++)
				Program.Iterations.Add($"x{i + 1} | {bestSolution[i]}");
			Program.Iterations.Add($"Max value | {bestValue}");
			Program.Iterations.Add($"Iterations | {iterationCount}");
		}
	}
}
