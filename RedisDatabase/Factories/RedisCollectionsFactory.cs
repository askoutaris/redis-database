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

	public partial class RedisCollectionsFactory : IRedisCollectionsFactory
	{
		private readonly BackgroundExpirationUpdater _defaultExpirationUpdater;
		private readonly ConcurrentDictionary<string, ICollectionBuilder> _builders;
		private readonly ILoggerFactory _loggerFactory;

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
