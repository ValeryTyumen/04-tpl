using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JapaneseCrossword
{
	internal enum Line
	{
		Row,
		Column
	}

	internal class Update
	{
		public Line LineType { get; private set; }
		public int Index { get; private set; }

		public Update(bool row, int index)
		{
			LineType = row ? Line.Row : Line.Column;
			Index = index;
		}

		public bool IsRow()
		{
			return LineType == Line.Row;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() != GetType())
				return false;
			var other = (Update)obj;
			return other.Index == Index && other.LineType == LineType;
		}

		public override int GetHashCode()
		{
			return LineType.GetHashCode() ^ Index.GetHashCode();
		}
	}

	internal class ProbableValueLine
	{
		public bool[] CanBeBlack;
		public bool[] CanBeWhite;
		public int Length
		{
			get { return CanBeBlack.Length; }
		}

		public ProbableValueLine(int length)
		{
			CanBeBlack = Enumerable
				.Range(0, length)
				.Select(z => false)
				.ToArray();
			CanBeWhite = CanBeBlack.ToArray();
		}

		public void WritePrefix(ProbableValueLine prefix, int length)
		{
			var minimum = Math.Min(prefix.Length, Math.Min(Length, length));
			for (var i = 0; i < minimum; i++)
			{
				CanBeBlack[i] = prefix.CanBeBlack[i];
				CanBeWhite[i] = prefix.CanBeWhite[i];
			}
		}

		public void WritePrefix(ProbableValueLine prefix)
		{
			WritePrefix(prefix, prefix.Length);
		}

		public void DisjunctWithPrefix(ProbableValueLine prefix, int length)
		{
			var minimum = Math.Min(prefix.Length, Math.Min(Length, length));
			for (var i = 0; i < minimum; i++)
			{
				CanBeBlack[i] = (CanBeBlack[i] || prefix.CanBeBlack[i]);
				CanBeWhite[i] = (CanBeWhite[i] || prefix.CanBeWhite[i]);
			}
		}

		public void DisjunctWithPrefix(ProbableValueLine prefix)
		{
			DisjunctWithPrefix(prefix, prefix.Length);
		}

		public bool IsBlack(int index)
		{
			return CanBeBlack[index] && (!CanBeWhite[index]);
		}

		public bool IsWhite(int index)
		{
			return CanBeWhite[index] && (!CanBeBlack[index]);
		}

		public bool IsNone(int index)
		{
			return !(CanBeBlack[index] || CanBeWhite[index]);
		}
	}

	public class CrosswordMath
	{

		private Crossword _crossword;
		private Cell[,] _matrix;
		private HashSet<Update> _toUpdate;
		private object _locker;
		private bool _incorrectCrossword;


		public CrosswordMath(Crossword crossword)
		{
			_crossword = crossword;
			_matrix = new Cell[crossword.Rows.Count, crossword.Columns.Count];
			for (var i = 0; i < _matrix.GetLength(0); i++)
				for (var j = 0; j < _matrix.GetLength(1); j++)
					_matrix[i, j] = new Cell();
			_toUpdate = new HashSet<Update>(Enumerable
				.Range(0, crossword.Rows.Count)
				.Select(z => new Update(true, z))
				.Concat(Enumerable
					.Range(0, crossword.Columns.Count)
					.Select(z => new Update(false, z))));
			_locker = new object();
		}

		public SolutionResult Solve()
		{
			while (true)
			{
				var toUpdateBuffer = _toUpdate.ToArray();
				_toUpdate = new HashSet<Update>();
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

		private Cell GetCell(Update update, int index)
		{
			if (update.LineType == Line.Row)
				return _matrix[update.Index, index];
			return _matrix[index, update.Index];
		}

		private List<int> GetCrosswordLine(Update update)
		{
			if (update.LineType == Line.Row)
				return _crossword.Rows[update.Index];
			return _crossword.Columns[update.Index];
		}

		private void DynamicallyCheckAllLineSolutions(Update update, ProbableValueLine valueLine)
		{
			var length = valueLine.Length;
			var crosswordLine = GetCrosswordLine(update);
			var previousSolutions = GetDinamicallyPreviousSolutions(update, valueLine.Length);
			for (var i = 0; i <= length; i++)
				if (previousSolutions[i].ContainsKey(crosswordLine.Count))
				{
					var valid = true;
					for (var j = i; j < length; j++)
						if (j >= 0 && GetCell(update, j).State == State.Black)
							valid = false;
					if (!valid)
						continue;
					var previousSolution = previousSolutions[i][crosswordLine.Count];
					valueLine.DisjunctWithPrefix(previousSolution);
					for (var j = previousSolution.Length; j < length; j++)
						valueLine.CanBeWhite[j] = true;
				}
		}

		private List<Dictionary<int, ProbableValueLine>> GetDinamicallyPreviousSolutions(Update update, int maxLength)
		{
			var previousSolutions = new List<Dictionary<int, ProbableValueLine>>();
			for (var i = 0; i <= maxLength; i++)
				AddSolutionsWithLength(update, previousSolutions, i);
			return previousSolutions;
		}

		private void AddSolutionsWithLength(Update update, List<Dictionary<int, ProbableValueLine>> previousSolutions,
			int length)
		{
			previousSolutions.Add(new Dictionary<int, ProbableValueLine>());
			if (length == 0)
				previousSolutions[0][0] = new ProbableValueLine(0);
			for (var j = 0; j < length; j++)
				AddSolutionsWithPrefix(update, previousSolutions, length, j);
		}

		private void AddSolutionsWithPrefix(Update update, List<Dictionary<int, ProbableValueLine>> previousSolutions,
			int length, int prefixSolutionLength)
		{
			var crosswordLine = GetCrosswordLine(update);
			foreach (var key in previousSolutions[prefixSolutionLength].Keys.ToArray())
			{
				if (key < crosswordLine.Count)
				{
					var elementLength = crosswordLine[key];
					if ((prefixSolutionLength == 0 && length < elementLength) ||
							(prefixSolutionLength > 0 && prefixSolutionLength > length - elementLength - 1))
						continue;
					if (NewSolutionIsValid(update, length, prefixSolutionLength, elementLength))
					{
						var newSolution = new ProbableValueLine(length);
						newSolution.WritePrefix(previousSolutions[prefixSolutionLength][key], prefixSolutionLength);
						for (var k = prefixSolutionLength; k < length - elementLength; k++)
							newSolution.CanBeWhite[k] = true;
						for (var k = length - elementLength; k < length; k++)
							newSolution.CanBeBlack[k] = true;
						if (previousSolutions[length].ContainsKey(key + 1))
							previousSolutions[length][key + 1].DisjunctWithPrefix(newSolution, length);
						else
							previousSolutions[length][key + 1] = newSolution;
					}
				}
			}
		}

		private bool NewSolutionIsValid(Update update, int length, int prefixSolutionLength,
			int lastElementLength)
		{
			for (var k = prefixSolutionLength; k < length - lastElementLength; k++)
				if (GetCell(update, k).State == State.Black)
					return false;
			for (var k = length - lastElementLength; k < length; k++)
				if (GetCell(update, k).State == State.White)
					return false;
			return true;
		}

		private void Update(Update update)
		{
			var length = _matrix.GetLength(update.LineType == Line.Row ? 1 : 0);
			var valueLine = new ProbableValueLine(length);
			DynamicallyCheckAllLineSolutions(update, valueLine);
			for (var i = 0; i < length; i++)
			{
				var cell = GetCell(update, i);
				var updated = false;
				if (valueLine.IsBlack(i) && cell.State == State.Unknown)
				{
					cell.State = State.Black;
					updated = true;
				}
				if (valueLine.IsWhite(i) && cell.State == State.Unknown)
				{
					cell.State = State.White;
					updated = true;
				}
				if (updated)
					lock (_locker)
						_toUpdate.Add(new Update(!update.IsRow(), i));
				if (valueLine.IsNone(i))
					_incorrectCrossword = true;
			}
		}
	}
}