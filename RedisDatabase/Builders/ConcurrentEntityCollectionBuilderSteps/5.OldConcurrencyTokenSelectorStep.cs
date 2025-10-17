namespace RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps
{
	/// <summary>
	/// Fifth step in the concurrent entity collection builder flow: configures how to extract the existing concurrency token from an entity.
	/// This token is used for optimistic locking to detect concurrent modifications.
	/// </summary>
	public interface IOldConcurrencyTokenSelectorStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the function that extracts the current concurrency token from an entity.
		/// Returns null for new entities that haven't been saved yet.
		/// Example: entity => entity.Version to extract the version property.
		/// </summary>
		/// <param name="selector">The function to extract the existing concurrency token.</param>
		INewConcurrencyTokenSelectorStep<TKey, TEntity> WithOldConcurrencyTokenSelector(Func<TEntity, string?> selector);
	}

	class OldConcurrencyTokenSelectorStep<TKey, TEntity> : IOldConcurrencyTokenSelectorStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IConcurrentEntityCollectionBuilder<TKey, TEntity> _builder;

		public OldConcurrencyTokenSelectorStep(IConcurrentEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public INewConcurrencyTokenSelectorStep<TKey, TEntity> WithOldConcurrencyTokenSelector(Func<TEntity, string?> selector)
		{
			ArgumentNullException.ThrowIfNull(selector, nameof(selector));

			_builder.WithOldConcurrencyTokenSelector(selector);

			return new NewConcurrencyTokenSelectorStep<TKey, TEntity>(_builder);
		}
	}
}
