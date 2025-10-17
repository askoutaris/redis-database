//using Newtonsoft.Json;

namespace RedisDatabase.Logging
{
	public readonly struct LogData<T>
	{
		//private readonly JsonSerializerSettings? _serializerSettings;

		public T? Data { get; }

		public LogData(T? data)
		{
			Data = data;
		}

		//public LogData(T? data, JsonSerializerSettings serializerSettings)
		//{
		//	_serializerSettings = serializerSettings;
		//	Data = data;
		//}

		public override string? ToString()
		{
			throw new NotImplementedException();
			//return _serializerSettings is not null
			//	? Data?.ToJson(_serializerSettings)
			//	: Data?.ToJson();
		}
	}
}
