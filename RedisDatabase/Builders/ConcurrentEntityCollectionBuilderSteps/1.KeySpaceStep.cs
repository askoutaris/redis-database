namespace RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps
{
	/// <summary>
	/// First step in the concurrent entity collection builder flow: configures the Redis hash key prefix for entities.
	/// This prefix is used to form the Redis key that stores the entity and its concurrency token.
	/// </summary>
	public interface IKeySpaceStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the Redis key prefix for the entity hash.
		/// Example: For keySpace "orders", entity key "123", the Redis key will be "orders:123".
		/// </summary>
		/// <param name="keySpace">The Redis key prefix for entities.</param>
		ISerializerStep<TKey, TEntity> WithKeySpace(string keySpace);
	}

	class KeySpaceStep<TKey, TEntity> : IKeySpaceStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IConcurrentEntityCollectionBuilder<TKey, TEntity> _builder;

		public KeySpaceStep(IConcurrentEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public ISerializerStep<TKey, TEntity> WithKeySpace(string keySpace)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(keySpace, nameof(keySpace));

			_builder.WithKeySpace(keySpace);

			return new SerializerStep<TKey, TEntity>(_builder);
		}
	}
}
