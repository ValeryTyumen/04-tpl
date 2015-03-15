namespace JapaneseCrossword
{
	public class SolutionResult
	{
		public Cell[,] Matrix { get; private set; }
		public SolutionStatus Status { get; private set; }

		public SolutionResult(Cell[,] matrix, SolutionStatus status)
		{
			Matrix = matrix;
			Status = status;
		}
	}
}