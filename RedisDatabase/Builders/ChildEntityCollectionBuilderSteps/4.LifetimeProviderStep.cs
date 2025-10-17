using RedisDatabase.LifetimeProvider;

namespace RedisDatabase.Builders.ChildEntityCollectionBuilderSteps
{
	/// <summary>
	/// Fourth step in the child entity collection builder flow: configures how long parent hash keys should remain in Redis.
	/// The expiration applies to the entire parent hash containing all child entities.
	/// </summary>
	public interface ILifetimeProviderStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures a custom lifetime provider for dynamic expiration logic based on parent keys.
		/// Use this when expiration time needs to vary based on the parent entity.
		/// </summary>
		/// <param name="provider">The custom lifetime provider implementation.</param>
		IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider provider);

		/// <summary>
		/// Configures a fixed expiration time for all parent hash keys.
		/// </summary>
		/// <param name="expiration">The time-to-live for the parent hash.</param>
		/// <param name="extendExpirationOnReads">If true, reading a child extends the parent hash expiration (sliding window).</param>
		IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false);

		/// <summary>
		/// Configures the parent hash keys to never expire and persist indefinitely in Redis.
		/// </summary>
		IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> WithNoExpiration();
	}

	class LifetimeProviderStep<TParentKey, TChildKey, TEntity> : ILifetimeProviderStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> _builder;

		public LifetimeProviderStep(IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider provider)
		{
			ArgumentNullException.ThrowIfNull(provider, nameof(provider));

			_builder.WithCustomLifetimeProvider(provider);

			return new UniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity>(_builder);
		}

		public IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false)
		{
			if (expiration <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero");

			_builder.WithDefaultLifetimeProvider(expiration, extendExpirationOnReads);

			return new UniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity>(_builder);
		}

		public IUniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity> WithNoExpiration()
		{
			_builder.WithNoExpiration();

			return new UniqueParentKeyFactoryStep<TParentKey, TChildKey, TEntity>(_builder);
		}
	}
}
