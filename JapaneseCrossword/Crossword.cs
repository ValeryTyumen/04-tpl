using System.Collections.Generic;

namespace JapaneseCrossword
{
	public class Crossword
	{
		public List<List<int>> Rows { get; private set; }
		public List<List<int>> Columns { get; private set; }

		public Crossword(List<List<int>> rows, List<List<int>> columns)
		{
			Rows = rows;
			Columns = columns;
		}
	}
}