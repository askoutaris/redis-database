using RedisDatabase.Builders;
using RedisDatabase.Builders.ChildEntityCollectionBuilderSteps;
using RedisDatabase.Collections;

namespace RedisDatabase.Factories
{
	public partial interface IRedisCollectionsFactory
	{
		/// <summary>
		/// Registers a child entity collection for hierarchical data structures during startup for later runtime retrieval.
		/// Prevents duplicate registrations and caches the configured builder.
		/// </summary>
		/// <typeparam name="TParentKey">The parent entity key type.</typeparam>
		/// <typeparam name="TChildKey">The child entity key type.</typeparam>
		/// <typeparam name="TEntity">The child entity type to cache.</typeparam>
		/// <param name="configure">Configuration function using step builder pattern for compile-time validation.</param>
		/// <param name="name">Optional name for multiple registrations of the same types. Defaults to "default".</param>
		void RegisterChildEntityCollection<TParentKey, TChildKey, TEntity>(Func<IKeySpaceStep<TParentKey, TChildKey, TEntity>, IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity>> configure, string name = "default")
			where TParentKey : notnull
			where TEntity : class;

		/// <summary>
		/// Retrieves a configured child entity collection instance for hierarchical storage using the cached builder.
		/// Requires prior registration via <see cref="RegisterChildEntityCollection{TParentKey, TChildKey, TEntity}"/>.
		/// </summary>
		/// <typeparam name="TParentKey">The parent entity key type.</typeparam>
		/// <typeparam name="TChildKey">The child entity key type.</typeparam>
		/// <typeparam name="TEntity">The child entity type to cache.</typeparam>
		/// <param name="context">The Redis context for collection operations.</param>
		/// <param name="name">Optional name to retrieve a specific registered collection. Defaults to "default".</param>
		/// <returns>A configured <see cref="IChildEntityCollection{TParentKey, TChildKey, TEntity}"/> instance.</returns>
		IChildEntityCollection<TParentKey, TChildKey, TEntity> GetChildEntityCollection<TParentKey, TChildKey, TEntity>(IRedisContext context, string name = "default")
			where TParentKey : notnull
			where TEntity : class;
	}

	partial class RedisCollectionsFactory : IRedisCollectionsFactory
	{
		public void RegisterChildEntityCollection<TParentKey, TChildKey, TEntity>(Func<IKeySpaceStep<TParentKey, TChildKey, TEntity>, IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity>> configure, string name = "default")
			where TParentKey : notnull
			where TEntity : class
		{
			ArgumentNullException.ThrowIfNull(configure, nameof(configure));

			var key = GetBuilderKey<TParentKey, TChildKey, TEntity>(name);

			if (_builders.ContainsKey(key))
				throw new InvalidOperationException($"ChildEntityCollection<{typeof(TParentKey).Name}, {typeof(TChildKey).Name}, {typeof(TEntity).Name}> with name '{name}' is already registered.");

			var initialBuilder = new ChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity>(_defaultExpirationUpdater, _loggerFactory);
			var initialStep = new KeySpaceStep<TParentKey, TChildKey, TEntity>(initialBuilder);
			var configuredBuilder = configure(initialStep);

			_builders[key] = configuredBuilder;
		}

		public IChildEntityCollection<TParentKey, TChildKey, TEntity> GetChildEntityCollection<TParentKey, TChildKey, TEntity>(IRedisContext context, string name = "default")
			where TParentKey : notnull
			where TEntity : class
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			var key = GetBuilderKey<TParentKey, TChildKey, TEntity>(name);

			if (!_builders.TryGetValue(key, out var builderObj))
				throw new InvalidOperationException($"ChildEntityCollection<{typeof(TParentKey).Name}, {typeof(TChildKey).Name}, {typeof(TEntity).Name}> with name '{name}' is not registered. Call RegisterChildEntityCollection first.");

			if (builderObj is not ChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
				throw new InvalidOperationException($"Registered builder is of type {builderObj.GetType()} instead of {typeof(ChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity>)}");

			return builder.Build(context);
		}

	}
}
