namespace RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps
{
	/// <summary>
	/// Final step in the concurrent entity collection builder flow: configures how to generate a new concurrency token when saving an entity.
	/// The new token is stored in Redis and should be different from the old token to enable conflict detection.
	/// </summary>
	public interface INewConcurrencyTokenSelectorStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the function that generates a new concurrency token when saving an entity.
		/// Must return a unique value for each save operation.
		/// Example: entity => Guid.NewGuid().ToString() to generate a new GUID for each save.
		/// </summary>
		/// <param name="selector">The function to generate new concurrency tokens.</param>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithNewConcurrencyTokenSelector(Func<TEntity, string> selector);
	}

	class NewConcurrencyTokenSelectorStep<TKey, TEntity> : INewConcurrencyTokenSelectorStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IConcurrentEntityCollectionBuilder<TKey, TEntity> _builder;

		public NewConcurrencyTokenSelectorStep(IConcurrentEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithNewConcurrencyTokenSelector(Func<TEntity, string> selector)
		{
			ArgumentNullException.ThrowIfNull(selector, nameof(selector));

			_builder.WithNewConcurrencyTokenSelector(selector);

			return _builder;
		}
	}
}
