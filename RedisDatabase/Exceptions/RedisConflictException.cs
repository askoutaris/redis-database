using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Exceptions
{
	/// <summary>
	/// Exception thrown when a Redis transaction fails due to optimistic concurrency conflicts.
	/// Indicates version mismatches or condition failures during transactional operations.
	/// </summary>
	[ExcludeFromCodeCoverage]
	public sealed class RedisConflictException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the RedisConflictException with the specified message.
		/// </summary>
		/// <param name="message">The error message describing the conflict.</param>
		public RedisConflictException(string message) : base(message) { }
	}
}
