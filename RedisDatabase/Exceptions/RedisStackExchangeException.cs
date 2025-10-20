using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Exceptions
{
	/// <summary>
	/// Exception that wraps all StackExchange.Redis library exceptions.
	/// Indicates infrastructure failures such as connection issues, network timeouts, or server errors.
	/// </summary>
	[ExcludeFromCodeCoverage]
	public sealed class RedisStackExchangeException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the RedisStackExchangeException with a message and inner exception.
		/// </summary>
		/// <param name="message">The error message describing the infrastructure failure.</param>
		/// <param name="innerException">The original StackExchange.Redis exception.</param>
		public RedisStackExchangeException(string message, Exception innerException) : base(message, innerException) { }
	}
}
