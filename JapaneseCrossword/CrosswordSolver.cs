using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace JapaneseCrossword
{
	public class CrosswordSolver : ICrosswordSolver
	{
		private bool ReadCrossword(string inputFilePath, out Crossword crossword)
		{
			string[] lines;
			try
			{
				lines = File.ReadAllLines(inputFilePath);
			}
			catch
			{
				crossword = new Crossword(new List<List<int>>(), new List<List<int>>());
				return false;
			}
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
			crossword = new Crossword(rows, columns);
			return true;
		}

		private bool WriteResult(string outputFilePath, Cell[,] matrix)
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
				return false;
			}
			return true;
		}

		public SolutionStatus Solve(string inputFilePath, string outputFilePath)
		{
			var filenameRegex = new Regex(@"[\w\-. ]+$");
			if (!(filenameRegex.IsMatch(inputFilePath, 0) && File.Exists(inputFilePath)))
				return SolutionStatus.BadInputFilePath;
			if (!filenameRegex.IsMatch(outputFilePath, 0))
				return SolutionStatus.BadOutputFilePath;
			string[] lines;
			Crossword crossword;
			if (! ReadCrossword(inputFilePath, out crossword))
				return SolutionStatus.BadInputFilePath;
			var crosswordMath = new CrosswordMath(crossword);
			var result = crosswordMath.Solve();
			if (!WriteResult(outputFilePath, result.Matrix))
				return SolutionStatus.BadOutputFilePath;
			return result.Status;
		}
	}
}