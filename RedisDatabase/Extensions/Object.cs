using RedisDatabase.Logging;

namespace RedisDatabase.Extensions
{
	/// <summary>
	/// Extension methods for object types to support logging.
	/// </summary>
	public static class ObjectExtensions
	{
		/// <summary>
		/// Converts an object to a LogData wrapper for structured logging with JSON serialization.
		/// </summary>
		/// <typeparam name="T">The type of the object to wrap.</typeparam>
		/// <param name="obj">The object to convert.</param>
		/// <returns>A LogData wrapper containing the object for logging.</returns>
		public static LogData<T> ToLogData<T>(this T? obj)
			=> new(obj);
	}
}
