using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JapaneseCrossword
{
	public class CrosswordMath
	{

		private Crossword _crossword;
		private Cell[,] _matrix;
		private List<Tuple<bool, int>> _toUpdate;
		private object _locker;
		private bool _incorrectCrossword;


		public CrosswordMath(Crossword crossword)
		{
			_crossword = crossword;
			_matrix = new Cell[crossword.Rows.Count, crossword.Columns.Count];
			for (var i = 0; i < _matrix.GetLength(0); i++)
				for (var j = 0; j < _matrix.GetLength(1); j++)
					_matrix[i, j] = new Cell();
			_toUpdate = Enumerable
				.Range(0, crossword.Rows.Count)
				.Select(z => Tuple.Create(true, z))
				.ToList();
			_locker = new object();
		}

		public SolutionResult Solve()
		{
			while (true)
			{
				var toUpdateBuffer = _toUpdate.ToArray();
				_toUpdate = new List<Tuple<bool, int>>();
				var tasks = new List<Task>();
				foreach (var update in toUpdateBuffer)
				{
					var localUpdate = update;
					tasks.Add(Task.Run(() => Update(localUpdate)));
				}
				Task.WaitAll(tasks.ToArray());
				if (_toUpdate.Count == 0 || _incorrectCrossword)
					break;
			}
			var status = SolutionStatus.Solved;
			if (_incorrectCrossword)
				status = SolutionStatus.IncorrectCrossword;
			foreach (var cell in _matrix)
				if (cell.State == State.Unknown)
					status = SolutionStatus.PartiallySolved;
			return new SolutionResult(_matrix, status);
		}

		private Cell GetCell(Tuple<bool, int> update, int index)
		{
			if (update.Item1)
				return _matrix[update.Item2, index];
			return _matrix[index, update.Item2];
		}

		private List<int> GetCrosswordLine(Tuple<bool, int> update)
		{
			if (update.Item1)
				return _crossword.Rows[update.Item2];
			return _crossword.Columns[update.Item2];
		}

		private void CheckAllLineSolutions(Tuple<bool, int> update, bool[] canBeBlack, bool[] canBeWhite)
		{
			var length = _matrix.GetLength(0);
			if (update.Item1)
				length = _matrix.GetLength(1);
			var solutionBase = Enumerable.Range(0, length)
				.Select(z => false)
				.ToArray();
			foreach (var solution in GetAllLineSolutions(update, solutionBase, 0, 0))
			{
				for (var i = 0; i < length; i++)
				{
					canBeBlack[i] = (canBeBlack[i] || solution[i]);
					canBeWhite[i] = (canBeWhite[i] || (!solution[i]));
				}
			}
		}

		private IEnumerable<bool[]> GetAllLineSolutions(Tuple<bool, int> update, 
			bool[] solutionBase, int lineStart, int lineTaskStart)
		{
			var crosswordLine = GetCrosswordLine(update);
			if (lineTaskStart == crosswordLine.Count) //MANAGE
			{
				var valid = true;
				for (var i = lineStart - 1; i < solutionBase.Length; i++)
					if (GetCell(update, i).State == State.Black)
						valid = false;
				if (valid)
					yield return solutionBase;
			}
			else
			{
				var minTaskLength = crosswordLine.Skip(lineTaskStart).Sum() + crosswordLine.Count - lineTaskStart - 1;
				for (var i = lineStart; i <= solutionBase.Length - minTaskLength; i++)
				{
					if (i != 0 && GetCell(update, i - 1).State == State.Black)
						break;
					var valid = true;
					for (var j = i; j < i + crosswordLine[lineTaskStart]; j++)
						if (GetCell(update, j).State == State.White)
						{
							valid = false;
							break;
						}
						else
							solutionBase[j] = true;
					if (valid)
						foreach (var solution in
							GetAllLineSolutions(update, solutionBase, i + crosswordLine[lineTaskStart] + 1, lineTaskStart + 1))
							yield return solution;
					for (var j = i; j < solutionBase.Length; j++)
						solutionBase[j] = false;
				}
			}
		}

		private void Update(Tuple<bool, int> update)
		{
			var length = _matrix.GetLength(0);
			if (update.Item1)
				length = _matrix.GetLength(1);
			var canBeBlack = Enumerable.Range(0, length).Select(z => false).ToArray();
			var canBeWhite = Enumerable.Range(0, length).Select(z => false).ToArray();
			CheckAllLineSolutions(update, canBeBlack, canBeWhite);
			for (var i = 0; i < length; i++)
			{
				var cell = GetCell(update, i);
				var updated = false;
				if (canBeBlack[i] && (!canBeWhite[i]) && cell.State == State.Unknown)
				{
					cell.State = State.Black;
					updated = true;
				}
				if ((!canBeBlack[i]) && canBeWhite[i] && cell.State == State.Unknown)
				{
					cell.State = State.White;
					updated = true;
				}
				if (updated)
					lock (_locker)
						_toUpdate.Add(Tuple.Create(!update.Item1, i));
				if ((!canBeBlack[i]) && (!canBeWhite[i]))
					lock (_locker)
						_incorrectCrossword = true;
			}
		}
	}
}