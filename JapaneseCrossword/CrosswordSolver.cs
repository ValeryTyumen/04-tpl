using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace JapaneseCrossword
{
	public class CrosswordSolver : ICrosswordSolver
	{
		public Crossword GetCrossword(string[] lines)
		{
			var rows = new List<List<int>>();
			var columns = new List<List<int>>();

			var rowLines = true;
			for (var i = 1; i < lines.Length; i++)
			{
				if (lines[i].StartsWith("column"))
				{
					rowLines = false;
					continue;
				}
				var values = lines[i]
					.Split(' ')
					.Select(int.Parse)
					.ToList();
				if (rowLines)
					rows.Add(values);
				else
					columns.Add(values);

			}
			return new Crossword(rows, columns);
		}

		private SolutionStatus WriteResult(string outputFilePath, Cell[,] matrix)
		{
			var lines = Enumerable
				.Range(0, matrix.GetLength(0))
				.Select(rowIndex => Enumerable
					.Range(0, matrix.GetLength(1))
					.Select(columnIndex => matrix[rowIndex, columnIndex])
					.Select(x => x.GetChar())
					.Aggregate((a, e) => a + e))
				.ToArray();
			try
			{
				File.WriteAllLines(outputFilePath, lines);
			}
			catch
			{
				return SolutionStatus.BadOutputFilePath;
			}
			return SolutionStatus.Solved;
		}

		public SolutionStatus Solve(string inputFilePath, string outputFilePath)
		{
			var filenameRegex = new Regex(@"[\w\-. ]+$");
	        if (! (filenameRegex.IsMatch(inputFilePath, 0) && File.Exists(inputFilePath)))
					return SolutionStatus.BadInputFilePath;
			if (! filenameRegex.IsMatch(outputFilePath, 0))
		        return SolutionStatus.BadOutputFilePath;
			string[] lines;
			try
			{
				lines = File.ReadAllLines(inputFilePath);
			}
			catch
			{
				return SolutionStatus.BadInputFilePath;
			}
			var crossword = GetCrossword(lines);
			var crosswordMath = new CrosswordMath(crossword);
			var result = crosswordMath.Solve();
			var writeStatus = WriteResult(outputFilePath, result.Matrix);
			if (writeStatus == SolutionStatus.BadOutputFilePath)
				return writeStatus;
			return result.Status;
        }
    }
}