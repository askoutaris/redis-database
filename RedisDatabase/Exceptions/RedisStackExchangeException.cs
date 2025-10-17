using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Exceptions
{
	[ExcludeFromCodeCoverage]
	public sealed class RedisStackExchangeException : Exception
	{
		public RedisStackExchangeException(string message, Exception innerException) : base(message, innerException) { }
	}
}
