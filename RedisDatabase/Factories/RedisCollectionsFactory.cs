using System.Collections.Concurrent;
using RedisDatabase.Builders;
using RedisDatabase.ExpirationUpdaters;
using StackExchange.Redis;
using RedisDatabase.PeriodicTriggers;

namespace RedisDatabase.Factories
{
	/// <summary>
	/// Factory for registering and retrieving Redis collection instances using a registration-based pattern for performance optimization.
	/// Implementations are split across partial classes for maintainability.
	/// </summary>
	public partial interface IRedisCollectionsFactory
	{
	}

	/// <summary>
	/// Factory for creating and managing Redis collections with registration-based configuration caching.
	/// </summary>
	public partial class RedisCollectionsFactory : IRedisCollectionsFactory
	{
		private readonly BackgroundExpirationUpdater _defaultExpirationUpdater;
		private readonly ConcurrentDictionary<string, ICollectionBuilder> _builders;
		private readonly ILoggerFactory _loggerFactory;

		/// <summary>
		/// Initializes a new instance of the RedisCollectionsFactory with the specified Redis connection and factories.
		/// </summary>
		/// <param name="multiplexer">The Redis connection multiplexer.</param>
		/// <param name="triggerFactory">Factory for creating periodic triggers.</param>
		/// <param name="loggerFactory">Factory for creating loggers.</param>
		public RedisCollectionsFactory(IConnectionMultiplexer multiplexer, IPeriodicTriggerFactory triggerFactory, ILoggerFactory loggerFactory)
		{
			var trigger = triggerFactory.Create<BackgroundExpirationUpdater>(TimeSpan.FromSeconds(5));
			var scheduler = new PriorityQueueScheduler<RedisKey>();

			_defaultExpirationUpdater = new BackgroundExpirationUpdater(multiplexer, scheduler, trigger);
			_loggerFactory = loggerFactory;

			_builders = [];
		}

		private static string GetBuilderKey<TKey, TEntity>(string name)
		{
			var keyType = typeof(TKey);
			var entityType = typeof(TEntity);
			return $"{keyType.FullName}|{entityType.FullName}#{name}";
		}

		private static string GetBuilderKey<TParentKey, TChildKey, TEntity>(string name)
		{
			var parentKeyType = typeof(TParentKey);
			var childKeyType = typeof(TChildKey);
			var entityType = typeof(TEntity);
			return $"{parentKeyType.FullName}|{childKeyType.FullName}|{entityType.FullName}#{name}";
		}

		private static string GetBuilderKey<TType>(string name)
		{
			var type = typeof(TType);
			return $"{type.FullName!}#{name}";
		}
	}
}
