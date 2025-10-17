namespace RedisDatabase.Builders.EntityCollectionBuilderSteps
{
	/// <summary>
	/// Final step in the entity collection builder flow: configures how entity keys are converted to Redis key strings.
	/// The factory transforms the entity key into a unique string identifier used in the Redis string key.
	/// </summary>
	public interface IUniqueKeyFactoryStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the function that converts entity keys to unique Redis key strings.
		/// Example: For entity key 123, factory might return "123" to create Redis key "users:123".
		/// </summary>
		/// <param name="factory">The function to convert entity keys to strings.</param>
		IEntityCollectionBuilder<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> factory);
	}

	class UniqueKeyFactoryStep<TKey, TEntity> : IUniqueKeyFactoryStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IEntityCollectionBuilder<TKey, TEntity> _builder;

		public UniqueKeyFactoryStep(IEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> factory)
		{
			ArgumentNullException.ThrowIfNull(factory, nameof(factory));

			_builder.WithUniqueKeyFactory(factory);

			return _builder;
		}
	}
}
