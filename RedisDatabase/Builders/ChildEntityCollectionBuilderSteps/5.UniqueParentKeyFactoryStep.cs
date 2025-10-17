namespace RedisDatabase.Builders.ChildEntityCollectionBuilderSteps
{
	/// <summary>
	/// Fifth step in the child entity collection builder flow: configures how parent keys are converted to Redis key strings.
	/// The factory transforms the parent key into a unique string identifier used in the Redis key.
	/// </summary>
	public interface IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the function that converts parent keys to unique Redis key strings.
		/// Example: For parent key 123, factory might return "123" to create Redis key "orders:123".
		/// </summary>
		/// <param name="factory">The function to convert parent keys to strings.</param>
		IUniqueChildKeyFactoryStep<TParentKey, TChildKey, TEntity> WithUniqueParentKeyFactory(Func<TParentKey, string> factory);
	}

	class UniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> : IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> _builder;

		public UniqueParentKeyFactoryStep(IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IUniqueChildKeyFactoryStep<TParentKey, TChildKey, TEntity> WithUniqueParentKeyFactory(Func<TParentKey, string> factory)
		{
			ArgumentNullException.ThrowIfNull(factory, nameof(factory));

			_builder.WithUniqueParentKeyFactory(factory);

			return new UniqueChildKeyFactoryStep<TParentKey, TChildKey, TEntity>(_builder);
		}
	}
}
