using RedisDatabase.Serializers;
using StackExchange.Redis;

namespace RedisDatabase.Adapters
{
	interface IResultReader
	{
		TType? Read<TType>(IRedisContext context, IRedisSerializer serializer, RedisKey key, Task<RedisValue> task) where TType : class;
	}

	class ResultReader : IResultReader
	{
		private readonly ILogger _logger;

		public ResultReader(ILogger logger)
		{
			_logger = logger;
		}

		public TType? Read<TType>(IRedisContext context, IRedisSerializer serializer, RedisKey key, Task<RedisValue> task) where TType : class
		{
			if (task.Result.IsNull)
				return null;

			try
			{
				return serializer.Deserialize<TType>(task.Result!);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deserializing key {key}", key);

				context.Database.KeyDelete(key);

				return null;
			}
		}
	}
}
