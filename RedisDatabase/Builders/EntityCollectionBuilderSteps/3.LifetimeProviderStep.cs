using RedisDatabase.LifetimeProvider;

namespace RedisDatabase.Builders.EntityCollectionBuilderSteps
{
	/// <summary>
	/// Third step in the entity collection builder flow: configures how long entity keys should remain in Redis.
	/// The expiration applies to the Redis string key containing the serialized entity.
	/// </summary>
	public interface ILifetimeProviderStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures a custom lifetime provider for dynamic expiration logic based on entity keys.
		/// Use this when expiration time needs to vary based on the entity.
		/// </summary>
		/// <param name="provider">The custom lifetime provider implementation.</param>
		IUniqueKeyFactoryStep<TKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider provider);

		/// <summary>
		/// Configures a fixed expiration time for all entity keys.
		/// </summary>
		/// <param name="expiration">The time-to-live for the entity key.</param>
		/// <param name="extendExpirationOnReads">If true, reading an entity extends the key expiration (sliding window).</param>
		IUniqueKeyFactoryStep<TKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false);

		/// <summary>
		/// Configures the entity keys to never expire and persist indefinitely in Redis.
		/// </summary>
		IUniqueKeyFactoryStep<TKey, TEntity> WithNoExpiration();
	}

	class LifetimeProviderStep<TKey, TEntity> : ILifetimeProviderStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		private readonly IEntityCollectionBuilder<TKey, TEntity> _builder;

		public LifetimeProviderStep(IEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public IUniqueKeyFactoryStep<TKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider provider)
		{
			ArgumentNullException.ThrowIfNull(provider, nameof(provider));

			_builder.WithCustomLifetimeProvider(provider);

			return new UniqueKeyFactoryStep<TKey, TEntity>(_builder);
		}

		public IUniqueKeyFactoryStep<TKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false)
		{
			if (expiration <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero");

			_builder.WithDefaultLifetimeProvider(expiration, extendExpirationOnReads);

			return new UniqueKeyFactoryStep<TKey, TEntity>(_builder);
		}

		public IUniqueKeyFactoryStep<TKey, TEntity> WithNoExpiration()
		{
			_builder.WithNoExpiration();

			return new UniqueKeyFactoryStep<TKey, TEntity>(_builder);
		}
	}
}
