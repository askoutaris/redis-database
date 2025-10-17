using RedisDatabase.Adapters;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using StackExchange.Redis;

namespace RedisDatabase.Collections
{
	class EntityCollection<TKey, TEntity> : IEntityCollection<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		private readonly string _keySpace;
		private readonly IStringAdapter _adapter;
		private readonly ILifetimeProvider _lifetimeProvider;
		private readonly IExpirationUpdater _expirationUpdater;
		private readonly Func<TKey, string> _uniqueKeyFactory;

		public EntityCollection(
			string keySpace,
			IStringAdapter adapter,
			ILifetimeProvider lifetimeProvider,
			IExpirationUpdater expirationUpdater,
			Func<TKey, string> uniqueKeyFactory)
		{
			_keySpace = keySpace;
			_adapter = adapter;
			_lifetimeProvider = lifetimeProvider;
			_expirationUpdater = expirationUpdater;
			_uniqueKeyFactory = uniqueKeyFactory;
		}

		public async Task<TEntity?> TryGet(TKey key)
		{
			var cacheKey = GetCacheKey(key);

			var entity = await _adapter.TryGet<TEntity>(cacheKey);

			var lifetime = _lifetimeProvider.GetCachingExpiration(key);

			if (lifetime.ExtendExpirationOnReads)
				_expirationUpdater.UpdateLifetime(cacheKey, lifetime.Expiration);

			return entity;
		}

		public ReadResult<TEntity> TryRead(TKey key)
		{
			var cacheKey = GetCacheKey(key);

			var read = _adapter.TryRead<TEntity>(cacheKey);

			var lifetime = _lifetimeProvider.GetCachingExpiration(key);

			if (lifetime.ExtendExpirationOnReads)
				_expirationUpdater.UpdateLifetime(cacheKey, lifetime.Expiration);

			return read;
		}

		public void Set(TKey key, TEntity entity)
		{
			var cacheKey = GetCacheKey(key);

			var lifetime = _lifetimeProvider.GetCachingExpiration(key);

			_adapter.Set(cacheKey, entity, lifetime.Expiration);
		}

		public void Remove(TKey key)
		{
			var cacheKey = GetCacheKey(key);

			_adapter.Remove(cacheKey);
		}

		private RedisKey GetCacheKey(TKey key)
		{
			var uniqueKey = _uniqueKeyFactory(key);

			return $"{_keySpace}|{uniqueKey}";
		}
	}
}
