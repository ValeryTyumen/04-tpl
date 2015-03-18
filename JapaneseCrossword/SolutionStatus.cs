namespace JapaneseCrossword
{
	public enum SolutionStatus
	{
		/// <description>
		/// Путь до исходного файла некорректен (невалидный путь, нет доступа, исходный файл не существует или произошла неизвестная ошибка в процессе чтения)
		/// </description>
		BadInputFilePath,

		/// <description>
		/// Путь до выходного файла некорректен (невалидный путь, нет доступа или произошла неизвестная ошибка в процессе записи)
		/// </description>
		BadOutputFilePath,

		/// <description>
		/// Исходный кроссворд некорректен
		/// </description>
		IncorrectCrossword,

		/// <description>
		/// Кроссворд решён частично
		/// </description>
		PartiallySolved,

		/// <description>
		/// Кроссворд решён полностью
		/// </description>
		Solved
	}
}