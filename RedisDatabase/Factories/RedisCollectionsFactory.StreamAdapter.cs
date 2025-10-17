using RedisDatabase.Adapters;
using RedisDatabase.Builders;
using RedisDatabase.Builders.StreamAdapterBuilderSteps;

namespace RedisDatabase.Factories
{
	public partial interface IRedisCollectionsFactory
	{
		/// <summary>
		/// Registers a Redis Streams adapter configuration during startup for later runtime retrieval.
		/// Prevents duplicate registrations and caches the configured builder.
		/// </summary>
		/// <typeparam name="TType">The message type for stream entries.</typeparam>
		/// <param name="configure">Configuration function using step builder pattern for compile-time validation.</param>
		/// <param name="name">Optional name for multiple registrations of the same types. Defaults to "default".</param>
		void RegisterStreamAdapter<TType>(Func<IStreamKeyStep<TType>, IStreamAdapterBuilder<TType>> configure, string name = "default")
			where TType : class;

		/// <summary>
		/// Retrieves a configured stream adapter instance for Redis Streams operations using the cached builder.
		/// Requires prior registration via <see cref="RegisterStreamAdapter{TType}"/>.
		/// </summary>
		/// <typeparam name="TType">The message type for stream entries.</typeparam>
		/// <param name="context">The Redis context for stream operations.</param>
		/// <param name="name">Optional name to retrieve a specific registered adapter. Defaults to "default".</param>
		/// <returns>A configured <see cref="IStreamAdapter{TType}"/> instance.</returns>
		IStreamAdapter<TType> GetStreamAdapter<TType>(IRedisContext context, string name = "default")
			where TType : class;
	}

	public partial class RedisCollectionsFactory : IRedisCollectionsFactory
	{
		public void RegisterStreamAdapter<TType>(Func<IStreamKeyStep<TType>, IStreamAdapterBuilder<TType>> configure, string name = "default")
			where TType : class
		{
			ArgumentNullException.ThrowIfNull(configure, nameof(configure));

			var key = GetBuilderKey<TType>(name);

			if (_builders.ContainsKey(key))
				throw new InvalidOperationException($"StreamAdapter<{typeof(TType).Name}> with name '{name}' is already registered.");

			var initialBuilder = new StreamAdapterBuilder<TType>(_loggerFactory);
			var initialStep = new StreamKeyStep<TType>(initialBuilder);
			var configuredBuilder = configure(initialStep);

			_builders[key] = configuredBuilder;
		}

		public IStreamAdapter<TType> GetStreamAdapter<TType>(IRedisContext context, string name = "default")
			where TType : class
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			var key = GetBuilderKey<TType>(name);

			if (!_builders.TryGetValue(key, out var builderObj))
				throw new InvalidOperationException($"StreamAdapter<{typeof(TType).Name}> with name '{name}' is not registered. Call RegisterStreamAdapter first.");

			if (builderObj is not IStreamAdapterBuilder<TType> builder)
				throw new InvalidOperationException($"Registered stream adapter is of type {builderObj.GetType()} instead of {typeof(IStreamAdapterBuilder<TType>)}");

			return builder.Build(context);
		}
	}
}
