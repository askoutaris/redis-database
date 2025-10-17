using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Exceptions
{
	[ExcludeFromCodeCoverage]
	public sealed class RedisConflictException : Exception
	{
		public RedisConflictException(string message) : base(message) { }
	}
}
