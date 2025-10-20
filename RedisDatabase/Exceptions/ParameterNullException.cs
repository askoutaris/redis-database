using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Exceptions
{
	/// <summary>
	/// Exception thrown when a required parameter is null.
	/// </summary>
	[ExcludeFromCodeCoverage]
	public class ParameterNullException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the ParameterNullException with the specified message.
		/// </summary>
		/// <param name="message">The error message describing the null parameter.</param>
		public ParameterNullException(string? message) : base(message)
		{ }

		/// <summary>
		/// Throws a ParameterNullException if the specified argument is null.
		/// </summary>
		/// <param name="argument">The argument to check for null.</param>
		/// <param name="message">The error message to include in the exception if argument is null.</param>
		/// <exception cref="ParameterNullException">Thrown when argument is null.</exception>
		public static void ThrowIfNull([NotNull]object? argument, string? message = null)
		{
			if (argument is null)
				throw new ParameterNullException(message);
		}
	}
}
