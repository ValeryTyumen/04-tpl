namespace JapaneseCrossword
{
	public class Cell
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
}