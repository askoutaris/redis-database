using RedisDatabase.Builders;
using RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps;
using RedisDatabase.Collections;

namespace RedisDatabase.Factories
{
	public partial interface IRedisCollectionsFactory
	{
		/// <summary>
		/// Registers a concurrent entity collection with optimistic concurrency control during startup for later runtime retrieval.
		/// Prevents duplicate registrations and caches the configured builder.
		/// </summary>
		/// <typeparam name="TKey">The entity key type.</typeparam>
		/// <typeparam name="TEntity">The entity type to cache with concurrency control.</typeparam>
		/// <param name="configure">Configuration function using step builder pattern for compile-time validation.</param>
		/// <param name="name">Optional name for multiple registrations of the same types. Defaults to "default".</param>
		void RegisterConcurrentEntityCollection<TKey, TEntity>(Func<IKeySpaceStep<TKey, TEntity>, IConcurrentEntityCollectionBuilder<TKey, TEntity>> configure, string name = "default")
			where TKey : notnull
			where TEntity : class;

		/// <summary>
		/// Retrieves a configured concurrent entity collection instance with optimistic locking using the cached builder.
		/// Requires prior registration via <see cref="RegisterConcurrentEntityCollection{TKey, TEntity}"/>.
		/// </summary>
		/// <typeparam name="TKey">The entity key type.</typeparam>
		/// <typeparam name="TEntity">The entity type to cache with concurrency control.</typeparam>
		/// <param name="context">The Redis context for collection operations.</param>
		/// <param name="name">Optional name to retrieve a specific registered collection. Defaults to "default".</param>
		/// <returns>A configured <see cref="IEntityCollection{TKey, TEntity}"/> instance with concurrency control.</returns>
		IEntityCollection<TKey, TEntity> GetConcurrentEntityCollection<TKey, TEntity>(IRedisContext context, string name = "default")
			where TKey : notnull
			where TEntity : class;
	}

	partial class RedisCollectionsFactory : IRedisCollectionsFactory
	{
		public void RegisterConcurrentEntityCollection<TKey, TEntity>(Func<IKeySpaceStep<TKey, TEntity>, IConcurrentEntityCollectionBuilder<TKey, TEntity>> configure, string name = "default")
			where TKey : notnull
			where TEntity : class
		{
			ArgumentNullException.ThrowIfNull(configure, nameof(configure));

			var key = GetBuilderKey<TKey, TEntity>(name);

			if (_builders.ContainsKey(key))
				throw new InvalidOperationException($"ConcurrentEntityCollection<{typeof(TKey).Name}, {typeof(TEntity).Name}> with name '{name}' is already registered.");

			var initialBuilder = new ConcurrentEntityCollectionBuilder<TKey, TEntity>(_defaultExpirationUpdater, _loggerFactory);
			var initialStep = new KeySpaceStep<TKey, TEntity>(initialBuilder);
			var configuredBuilder = configure(initialStep);

			_builders[key] = configuredBuilder;
		}

		public IEntityCollection<TKey, TEntity> GetConcurrentEntityCollection<TKey, TEntity>(IRedisContext context, string name = "default")
			where TKey : notnull
			where TEntity : class
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			var key = GetBuilderKey<TKey, TEntity>(name);

			if (!_builders.TryGetValue(key, out var builderObj))
				throw new InvalidOperationException($"ConcurrentEntityCollection<{typeof(TKey).Name}, {typeof(TEntity).Name}> with name '{name}' is not registered. Call RegisterConcurrentEntityCollection first.");

			if (builderObj is not ConcurrentEntityCollectionBuilder<TKey, TEntity> builder)
				throw new InvalidOperationException($"Registered builder is of type {builderObj.GetType()} instead of {typeof(ConcurrentEntityCollectionBuilder<TKey, TEntity>)}");

			return builder.Build(context);
		}
	}
}
