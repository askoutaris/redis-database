using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Exceptions
{
	[ExcludeFromCodeCoverage]
	public class ParameterNullException : Exception
	{
		public ParameterNullException(string? message) : base(message)
		{ }

		public static void ThrowIfNull([NotNull]object? argument, string? message = null)
		{
			if (argument is null)
				throw new ParameterNullException(message);
		}
	}
}
