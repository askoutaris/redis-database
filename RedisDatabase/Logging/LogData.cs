using System.Text.Json;

namespace RedisDatabase.Logging
{
	/// <summary>
	/// Wrapper for structured logging that serializes data to JSON for log output.
	/// </summary>
	/// <typeparam name="T">The type of data to log.</typeparam>
	public readonly struct LogData<T>
	{
		private readonly JsonSerializerOptions? _serializerOptions;

		/// <summary>
		/// Gets the data to be logged.
		/// </summary>
		public T? Data { get; }

		/// <summary>
		/// Initializes a new LogData instance with the specified data and default serialization options.
		/// </summary>
		/// <param name="data">The data to log.</param>
		public LogData(T? data)
		{
			Data = data;
		}

		/// <summary>
		/// Initializes a new LogData instance with the specified data and custom serialization options.
		/// </summary>
		/// <param name="data">The data to log.</param>
		/// <param name="serializerOptions">Custom JSON serialization options.</param>
		public LogData(T? data, JsonSerializerOptions serializerOptions)
		{
			_serializerOptions = serializerOptions;
			Data = data;
		}

		/// <summary>
		/// Converts the data to a JSON string representation for logging.
		/// </summary>
		/// <returns>A JSON string representation of the data.</returns>
		public override string? ToString()
			=> JsonSerializer.Serialize(Data, _serializerOptions);
	}
}
