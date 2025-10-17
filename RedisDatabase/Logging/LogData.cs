using System.Text.Json;

namespace RedisDatabase.Logging
{
	public readonly struct LogData<T>
	{
		private readonly JsonSerializerOptions? _serializerOptions;
		public T? Data { get; }

		public LogData(T? data)
		{
			Data = data;
		}

		public LogData(T? data, JsonSerializerOptions serializerOptions)
		{
			_serializerOptions = serializerOptions;
			Data = data;
		}

		public override string? ToString()
			=> JsonSerializer.Serialize(Data, _serializerOptions);
	}
}
