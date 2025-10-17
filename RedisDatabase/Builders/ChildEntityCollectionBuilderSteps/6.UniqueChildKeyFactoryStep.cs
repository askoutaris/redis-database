namespace RedisDatabase.Builders.ChildEntityCollectionBuilderSteps
{
	/// <summary>
	/// Final step in the child entity collection builder flow: configures how child keys are converted to hash field names.
	/// The factory transforms the child key into a unique string identifier used as the hash field name.
	/// </summary>
	public interface IUniqueChildKeyFactoryStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the function that converts child keys to unique hash field name strings.
		/// Example: For child key "item5", factory might return "item5" to create hash field "item:item5".
		/// </summary>
		/// <param name="factory">The function to convert child keys to strings.</param>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithUniqueChildKeyFactory(Func<TChildKey, string> factory);
	}

	class UniqueChildKeyFactoryStep<TParentKey, TChildKey, TEntity> : IUniqueChildKeyFactoryStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> _builder;

		public UniqueChildKeyFactoryStep(IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithUniqueChildKeyFactory(Func<TChildKey, string> factory)
		{
			ArgumentNullException.ThrowIfNull(factory, nameof(factory));

			_builder.WithUniqueChildKeyFactory(factory);

			return _builder;
		}
	}
}
