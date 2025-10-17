namespace RedisDatabase.Builders.EntityCollectionBuilderSteps
{
	/// <summary>
	/// First step in the entity collection builder flow: configures the Redis string key prefix for entities.
	/// This prefix is used to form the Redis key that stores each serialized entity.
	/// </summary>
	public interface IKeySpaceStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the Redis key prefix for entities.
		/// Example: For keySpace "users", entity key "123", the Redis key will be "users:123".
		/// </summary>
		/// <param name="keySpace">The Redis key prefix for entities.</param>
		ISerializerStep<TKey, TEntity> WithKeySpace(string keySpace);
	}

	class KeySpaceStep<TKey, TEntity> : IKeySpaceStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IEntityCollectionBuilder<TKey, TEntity> _builder;

		public KeySpaceStep(IEntityCollectionBuilder<TKey, TEntity> builder)
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
