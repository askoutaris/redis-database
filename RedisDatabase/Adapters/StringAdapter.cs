using RedisDatabase.Serializers;
using StackExchange.Redis;

namespace RedisDatabase.Adapters
{
	/// <summary>
	/// Provides Redis string operations with object serialization and deferred execution through RedisContext.
	/// </summary>
	interface IStringAdapter
	{
		/// <summary>
		/// Immediately retrieves and deserializes an entity from a Redis string key without batching.
		/// </summary>
		/// <typeparam name="TType">The entity type to deserialize.</typeparam>
		/// <param name="key">The Redis key to read from.</param>
		/// <returns>A task containing the deserialized entity, or null if the key does not exist.</returns>
		Task<TType?> TryGet<TType>(RedisKey key) where TType : class;

		/// <summary>
		/// Queues a batched read operation to retrieve and deserialize an entity from a Redis string key.
		/// </summary>
		/// <typeparam name="TType">The entity type to deserialize.</typeparam>
		/// <param name="key">The Redis key to read from.</param>
		/// <returns>A <see cref="ReadResult{TType}"/> containing the deserialized entity, or null if the key does not exist.</returns>
		ReadResult<TType> TryRead<TType>(RedisKey key) where TType : class;

		/// <summary>
		/// Queues a command to serialize and store an entity as a Redis string with optional expiration.
		/// </summary>
		/// <typeparam name="TType">The entity type to serialize.</typeparam>
		/// <param name="key">The Redis key to write to.</param>
		/// <param name="entity">The entity to serialize and store.</param>
		/// <param name="expiration">Optional expiration time for the key.</param>
		void Set<TType>(RedisKey key, TType entity, TimeSpan? expiration);

		/// <summary>
		/// Queues a command to delete a Redis key.
		/// </summary>
		/// <param name="key">The Redis key to delete.</param>
		void Remove(RedisKey key);
	}

	class StringAdapter : IStringAdapter
	{
		private readonly IRedisContext _context;
		private readonly IRedisSerializer _serializer;
		private readonly IResultReader _reader;

		public StringAdapter(IRedisContext context, IRedisSerializer serializer, IResultReader reader)
		{
			_context = context;
			_serializer = serializer;
			_reader = reader;
		}

		public async Task<TType?> TryGet<TType>(RedisKey key) where TType : class
		{
			var value = await _context.Database.StringGetAsync(key);

			if (value.IsNull)
				return null;

			var obj = _serializer.Deserialize<TType>(value!);

			return obj;
		}

		public ReadResult<TType> TryRead<TType>(RedisKey key) where TType : class
		{
			var read = _context.AddBatch(x => x.StringGetAsync(key).ContinueWith(task => _reader.Read<TType>(_context, _serializer, key, task)));

			return new ReadResult<TType>(read);
		}

		public void Set<TType>(RedisKey key, TType entity, TimeSpan? expiration)
		{
			var value = _serializer.Serialize(entity);

			_context.AddCommand(db => db.StringSetAsync(
				key: key,
				value: value,
				expiry: expiration));
		}

		public void Remove(RedisKey key)
			=> _context.AddCommand(db => db.KeyDeleteAsync(key));
	}
}
