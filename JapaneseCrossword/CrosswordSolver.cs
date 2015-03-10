using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace JapaneseCrossword
{

	internal class CrosswordTask
	{
		public List<List<int>> Rows { get; private set; }
		public List<List<int>> Columns { get; private set; }

		public CrosswordTask(List<List<int>> rows, List<List<int>> columns)
		{
			Rows = rows;
			Columns = columns;
		}

		public static CrosswordTask ReadTask(string inputFilePath)
		{
			var lines = File.ReadAllLines(inputFilePath);
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
			return new CrosswordTask(rows, columns);
		}
	}

	internal enum State
	{
		Black,
		White, 
		Unknown
	}

	internal class Cell
	{
		public State State;

		public Cell()
		{
			State = State.Unknown;
		}

		public string GetChar()
		{
			if (State == State.Unknown)
				return "?";
			if (State == State.Black)
				return "*";
			return ".";
		}
	}

	public class CrosswordSolver : ICrosswordSolver
	{
		private readonly List<Task> _tasks;
		private bool _incorrectCrossword;
		private readonly object _locker;
		private readonly Regex _filenameRegex;

		public CrosswordSolver()
		{
			_tasks = new List<Task>();
			_incorrectCrossword = false;
			_locker = new object();
			_filenameRegex = new Regex(@"[\w\-. ]+$");
		}

		private void WriteResult(string outputFilePath, Cell[,] matrix)
		{
			File.WriteAllLines(outputFilePath, Enumerable
				.Range(0, matrix.GetLength(0))
				.Select(rowIndex => Enumerable
					.Range(0, matrix.GetLength(1))
					.Select(columnIndex => matrix[rowIndex, columnIndex])
					.Select(x => x.GetChar())
					.Aggregate((a, e) => a + e))
				.ToArray()
			);
		}

		public SolutionStatus Solve(string inputFilePath, string outputFilePath)
        {
	        if (! (_filenameRegex.IsMatch(inputFilePath, 0) && File.Exists(inputFilePath)))
		        return SolutionStatus.BadInputFilePath;
			if (! _filenameRegex.IsMatch(outputFilePath, 0))
		        return SolutionStatus.BadOutputFilePath;
	        var task = CrosswordTask.ReadTask(inputFilePath);
	        var matrix = new Cell[task.Rows.Count, task.Columns.Count];
			for (var i = 0; i < matrix.GetLength(0); i++)
				for (var j = 0; j < matrix.GetLength(1); j++)
					matrix[i, j] = new Cell();
	        for (var i = 0; i < task.Rows.Count; i++)
	        {
				var iLocal = i;
		        lock (_tasks)
		        {
					var threadTask = Task.Run(() => UpdateRowOrColumn(true, iLocal, matrix, task));
			        _tasks.Add(threadTask);
		        }
	        }
			var allTasks = Task.WhenAll(_tasks);
			allTasks.Wait();
			if (_incorrectCrossword)
				return SolutionStatus.IncorrectCrossword;
			WriteResult(outputFilePath, matrix);
			foreach(var cell in matrix)
				if (cell.State == State.Unknown)
					return SolutionStatus.PartiallySolved;
	        return SolutionStatus.Solved;
        }

		private Cell[] CopyRow(int index, Cell[,] matrix)
		{
			return Enumerable
				.Range(0, matrix.GetLength(1))
				.Select(z => matrix[index, z])
				.ToArray();
		}

		private Cell[] CopyColumn(int index, Cell[,] matrix)
		{
			return Enumerable
				.Range(0, matrix.GetLength(0))
				.Select(z => matrix[z, index])
				.ToArray();
		}

		private void CheckAllLineSolutions(Cell[] line, List<int> lineTask, bool[] canBeBlack, bool[] canBeWhite)
		{
			foreach (var solution in GetAllLineSolutions(line.Select(z => false).ToArray(), 0, lineTask, 0))
			{
				var valid = true;
				for (var i = 0; i < line.Length; i++)
				{
					if ((!solution[i]) && line[i].State == State.Black)
						valid = false;
					if (solution[i] && line[i].State == State.White)
						valid = false;
				}
				if (valid)
					for (var i = 0; i < line.Length; i++)
					{
						canBeBlack[i] = (canBeBlack[i] || solution[i]);
						canBeWhite[i] = (canBeWhite[i] || (!solution[i]));
					}
			}
		}

		private IEnumerable<bool[]> GetAllLineSolutions(bool[] line, int lineStart, List<int> lineTask, int lineTaskStart)
		{
			if (lineStart >= line.Length || lineTaskStart >= lineTask.Count)
			{
				yield return line;
				yield break;
			}
			var minTaskLength = lineTask.Skip(lineTaskStart).Sum() + lineTask.Count - lineTaskStart - 1;
			for (var i = lineStart; i <= line.Length - minTaskLength; i++)
			{
				var lineCopy = line.ToArray();
				for (var j = i; j < i + lineTask[lineTaskStart]; j++)
					lineCopy[j] = true;
				foreach (var solution in
						GetAllLineSolutions(lineCopy, i + lineTask[lineTaskStart] + 1, lineTask, lineTaskStart + 1))
					yield return solution;
			}
		}

		private Cell[] GetUpdates(Cell[] line, List<int> lineTask)
		{
			var result = line
				.Select(z => new Cell())
				.ToArray();
			var canBeBlack = line.Select(z => false).ToArray();
			var canBeWhite = line.Select(z => false).ToArray();
			CheckAllLineSolutions(line, lineTask, canBeBlack, canBeWhite);
			for (var i = 0; i < line.Length; i++)
			{
				if (canBeBlack[i] && (! canBeWhite[i]) && line[i].State == State.Unknown)
					result[i].State = State.Black;
				if ((!canBeBlack[i]) && canBeWhite[i] && line[i].State == State.Unknown)
					result[i].State = State.White;
				if ((!canBeBlack[i]) && (!canBeWhite[i]))
					lock (_locker)
						_incorrectCrossword = true;
			}
			return result;
		}

		private void UpdateRowOrColumn(bool row, int index, Cell[,] matrix, CrosswordTask task)
		{
			Cell[] updates;
			if (row)
				updates = GetUpdates(CopyRow(index, matrix), task.Rows[index]);
			else
				updates = GetUpdates(CopyColumn(index, matrix), task.Columns[index]);
			for (var i = 0; i < updates.Length; i++)
				if (updates[i].State != State.Unknown)
				{
					Cell updatedCell;
					if (row)
						updatedCell = matrix[index, i];
					else
						updatedCell = matrix[i, index];
					lock (updatedCell)
						updatedCell.State = updates[i].State;
				}
			for (var i = 0; i < updates.Length; i++)
				if (updates[i].State != State.Unknown)
				{
					var iLocal = i;
					Task.Run(() => UpdateRowOrColumn(!row, iLocal, matrix, task)).Wait();
				}
		}
    }
}