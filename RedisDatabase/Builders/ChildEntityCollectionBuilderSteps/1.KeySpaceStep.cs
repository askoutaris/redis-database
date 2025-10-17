namespace RedisDatabase.Builders.ChildEntityCollectionBuilderSteps
{
	/// <summary>
	/// First step in the child entity collection builder flow: configures the Redis hash key prefix for parent entities.
	/// This prefix is used to form the Redis key that stores all child entities for a parent.
	/// </summary>
	public interface IKeySpaceStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the Redis key prefix for the parent hash.
		/// Example: For keySpace "orders", parent key "123", the Redis key will be "orders:123".
		/// </summary>
		/// <param name="keySpace">The Redis key prefix for parent entities.</param>
		IChildKeyPrefixStep<TParentKey, TChildKey, TEntity> WithKeySpace(string keySpace);
	}

	class KeySpaceStep<TParentKey, TChildKey, TEntity> : IKeySpaceStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> _builder;

		public KeySpaceStep(IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IChildKeyPrefixStep<TParentKey, TChildKey, TEntity> WithKeySpace(string keySpace)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(keySpace, nameof(keySpace));

			_builder.WithKeySpace(keySpace);

			return new ChildKeyPrefixStep<TParentKey, TChildKey, TEntity>(_builder);
		}
	}
}
