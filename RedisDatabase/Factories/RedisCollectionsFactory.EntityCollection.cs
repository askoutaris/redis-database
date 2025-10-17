using RedisDatabase.Builders;
using RedisDatabase.Builders.EntityCollectionBuilderSteps;
using RedisDatabase.Collections;

namespace RedisDatabase.Factories
{
	public partial interface IRedisCollectionsFactory
	{
		/// <summary>
		/// Registers an entity collection configuration during startup for later runtime retrieval.
		/// Prevents duplicate registrations and caches the configured builder.
		/// </summary>
		/// <typeparam name="TKey">The entity key type.</typeparam>
		/// <typeparam name="TEntity">The entity type to cache.</typeparam>
		/// <param name="configure">Configuration function using step builder pattern for compile-time validation.</param>
		/// <param name="name">Optional name for multiple registrations of the same types. Defaults to "default".</param>
		void RegisterEntityCollection<TKey, TEntity>(Func<IKeySpaceStep<TKey, TEntity>, IEntityCollectionBuilder<TKey, TEntity>> configure, string name = "default")
			where TKey : notnull
			where TEntity : class;

		/// <summary>
		/// Retrieves a configured entity collection instance for the specified types using the cached builder.
		/// Requires prior registration via <see cref="RegisterEntityCollection{TKey, TEntity}"/>.
		/// </summary>
		/// <typeparam name="TKey">The entity key type.</typeparam>
		/// <typeparam name="TEntity">The entity type to cache.</typeparam>
		/// <param name="context">The Redis context for collection operations.</param>
		/// <param name="name">Optional name to retrieve a specific registered collection. Defaults to "default".</param>
		/// <returns>A configured <see cref="IEntityCollection{TKey, TEntity}"/> instance.</returns>
		IEntityCollection<TKey, TEntity> GetEntityCollection<TKey, TEntity>(IRedisContext context, string name = "default")
			where TKey : notnull
			where TEntity : class;
	}

	public partial class RedisCollectionsFactory : IRedisCollectionsFactory
	{
		public void RegisterEntityCollection<TKey, TEntity>(Func<IKeySpaceStep<TKey, TEntity>, IEntityCollectionBuilder<TKey, TEntity>> configure, string name = "default")
			where TKey : notnull
			where TEntity : class
		{
			ArgumentNullException.ThrowIfNull(configure, nameof(configure));

			var key = GetBuilderKey<TKey, TEntity>(name);

			if (_builders.ContainsKey(key))
				throw new InvalidOperationException($"EntityCollection<{typeof(TKey).Name}, {typeof(TEntity).Name}> with name '{name}' is already registered.");

			var initialBuilder = new EntityCollectionBuilder<TKey, TEntity>(_defaultExpirationUpdater, _loggerFactory);
			var initialStep = new KeySpaceStep<TKey, TEntity>(initialBuilder);
			var configuredBuilder = configure(initialStep);

			_builders[key] = configuredBuilder;
		}

		public IEntityCollection<TKey, TEntity> GetEntityCollection<TKey, TEntity>(IRedisContext context, string name = "default")
			where TKey : notnull
			where TEntity : class
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			var key = GetBuilderKey<TKey, TEntity>(name);

			if (!_builders.TryGetValue(key, out var builderObj))
				throw new InvalidOperationException($"EntityCollection<{typeof(TKey).Name}, {typeof(TEntity).Name}> with name '{name}' is not registered. Call RegisterEntityCollection first.");

			if (builderObj is not EntityCollectionBuilder<TKey, TEntity> builder)
				throw new InvalidOperationException($"Registered builder is of type {builderObj.GetType()} instead of {typeof(EntityCollectionBuilder<TKey, TEntity>)}");

			return builder.Build(context);
		}
	}
}
