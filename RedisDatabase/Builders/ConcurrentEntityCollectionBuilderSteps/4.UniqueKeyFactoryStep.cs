namespace RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps
{
	/// <summary>
	/// Fourth step in the concurrent entity collection builder flow: configures how entity keys are converted to Redis key strings.
	/// The factory transforms the entity key into a unique string identifier used in the Redis hash key.
	/// </summary>
	public interface IUniqueKeyFactoryStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the function that converts entity keys to unique Redis key strings.
		/// Example: For entity key 123, factory might return "123" to create Redis key "orders:123".
		/// </summary>
		/// <param name="factory">The function to convert entity keys to strings.</param>
		IOldConcurrencyTokenSelectorStep<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> factory);
	}

	class UniqueKeyFactoryStep<TKey, TEntity> : IUniqueKeyFactoryStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IConcurrentEntityCollectionBuilder<TKey, TEntity> _builder;

		public UniqueKeyFactoryStep(IConcurrentEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IOldConcurrencyTokenSelectorStep<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> factory)
		{
			ArgumentNullException.ThrowIfNull(factory, nameof(factory));

			_builder.WithUniqueKeyFactory(factory);

			return new OldConcurrencyTokenSelectorStep<TKey, TEntity>(_builder);
		}
	}
}
