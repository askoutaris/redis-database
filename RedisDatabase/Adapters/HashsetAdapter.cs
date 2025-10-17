using RedisDatabase.Serializers;
using StackExchange.Redis;

namespace RedisDatabase.Adapters
{
	/// <summary>
	/// Provides Redis hash operations with field-level object serialization and deferred execution through RedisContext.
	/// </summary>
	interface IHashsetAdapter
	{
		/// <summary>
		/// Immediately retrieves and deserializes an entity from a Redis hash field without batching.
		/// </summary>
		/// <typeparam name="TType">The entity type to deserialize.</typeparam>
		/// <param name="key">The Redis hash key.</param>
		/// <param name="fieldName">The hash field name to read from.</param>
		/// <returns>A task containing the deserialized entity, or null if the field does not exist.</returns>
		Task<TType?> TryGetField<TType>(RedisKey key, RedisValue fieldName) where TType : class;

		/// <summary>
		/// Queues a batched read operation to retrieve and deserialize an entity from a Redis hash field.
		/// </summary>
		/// <typeparam name="TType">The entity type to deserialize.</typeparam>
		/// <param name="key">The Redis hash key.</param>
		/// <param name="fieldName">The hash field name to read from.</param>
		/// <returns>A <see cref="ReadResult{TType}"/> containing the deserialized entity, or null if the field does not exist.</returns>
		ReadResult<TType> TryReadField<TType>(RedisKey key, RedisValue fieldName) where TType : class;

		/// <summary>
		/// Queues a command to serialize and store an entity in a Redis hash field.
		/// </summary>
		/// <typeparam name="TType">The entity type to serialize.</typeparam>
		/// <param name="key">The Redis hash key.</param>
		/// <param name="fieldName">The hash field name to write to.</param>
		/// <param name="entity">The entity to serialize and store.</param>
		void SetField<TType>(RedisKey key, RedisValue fieldName, TType entity);

		/// <summary>
		/// Queues a command to delete a specific field from a Redis hash.
		/// </summary>
		/// <param name="key">The Redis hash key.</param>
		/// <param name="fieldName">The hash field name to delete.</param>
		void RemoveField(RedisKey key, RedisValue fieldName);

		/// <summary>
		/// Queues a command to delete an entire Redis hash key.
		/// </summary>
		/// <param name="key">The Redis hash key to delete.</param>
		void Remove(RedisKey key);
	}

	class HashsetAdapter : IHashsetAdapter
	{
		private readonly IRedisContext _context;
		private readonly IRedisSerializer _serializer;
		private readonly IResultReader _reader;

		public HashsetAdapter(IRedisContext context, IRedisSerializer serializer, IResultReader reader)
		{
			_context = context;
			_serializer = serializer;
			_reader = reader;
		}

		public async Task<TType?> TryGetField<TType>(RedisKey key, RedisValue fieldName) where TType : class
		{
			var value = await _context.Database.HashGetAsync(key, fieldName);

			if (value.IsNull)
				return null;

			var obj = _serializer.Deserialize<TType>(value!);

			return obj;
		}

		public ReadResult<TType> TryReadField<TType>(RedisKey key, RedisValue fieldName) where TType : class
		{
			var read = _context.AddBatch(x => x.HashGetAsync(key, fieldName).ContinueWith(task => _reader.Read<TType>(_context, _serializer, key, task)));

			return new ReadResult<TType>(read);
		}

		public void SetField<TType>(RedisKey key, RedisValue fieldName, TType entity)
		{
			var value = _serializer.Serialize(entity);

			_context.AddCommand(db => db.HashSetAsync(
				key: key,
				hashField: fieldName,
				value: value));
		}

		public void RemoveField(RedisKey key, RedisValue fieldName)
			=> _context.AddCommand(db => db.HashDeleteAsync(
				key: key,
				hashField: fieldName));

		public void Remove(RedisKey key)
			=> _context.AddCommand(db => db.KeyDeleteAsync(key));
	}
}
