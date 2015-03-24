using System;
using System.IO;
using System.Text.RegularExpressions;

namespace JapaneseCrossword
{
	class Program
	{
		static void Main(string[] args)
		{
			var solver = new CrosswordSolver();
			var res = solver.Solve(@"TestFiles\SuperBig.txt", @"valera.txt");
			if (res == SolutionStatus.Solved)
				Console.WriteLine("OKAY");
			else
				Console.WriteLine("BAD");
			Console.ReadKey();
		}
	}
}
