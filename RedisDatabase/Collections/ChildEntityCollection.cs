using RedisDatabase.Adapters;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using StackExchange.Redis;

namespace RedisDatabase.Collections
{
	class ChildEntityCollection<TParentKey, TChildKey, TEntity> : IChildEntityCollection<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly string _keySpace;
		private readonly string _childKeyPrefix;
		private readonly IHashsetAdapter _adapter;
		private readonly ILifetimeProvider _lifetimeProvider;
		private readonly IExpirationUpdater _expirationUpdater;
		private readonly Func<TParentKey, string> _uniqueParentKeyFactory;
		private readonly Func<TChildKey, string> _uniqueChildKeyFactory;

		public ChildEntityCollection(
			string keySpace,
			string childKeyPrefix,
			IHashsetAdapter adapter,
			ILifetimeProvider lifetimeProvider,
			IExpirationUpdater expirationUpdater,
			Func<TParentKey, string> uniqueHashsetKeyFactory,
			Func<TChildKey, string> uniqueFieldKeyFactory)
		{
			_keySpace = keySpace;
			_childKeyPrefix = childKeyPrefix;
			_adapter = adapter;
			_lifetimeProvider = lifetimeProvider;
			_expirationUpdater = expirationUpdater;
			_uniqueParentKeyFactory = uniqueHashsetKeyFactory;
			_uniqueChildKeyFactory = uniqueFieldKeyFactory;
		}

		public async Task<TEntity?> TryGetChild(TParentKey parentKey, TChildKey childKey)
		{
			var parentCacheKey = GetParentKey(parentKey);
			var childCacheKey = GetChildKey(childKey);

			var entity = await _adapter.TryGetField<TEntity>(parentCacheKey, childCacheKey);

			TouchParent(parentKey);

			return entity;
		}

		public ReadResult<TEntity> TryReadChild(TParentKey parentKey, TChildKey childKey)
		{
			var parentCacheKey = GetParentKey(parentKey);
			var childCacheKey = GetChildKey(childKey);

			var read = _adapter.TryReadField<TEntity>(parentCacheKey, childCacheKey);

			TouchParent(parentKey);

			return read;
		}

		public void SetChild(TParentKey parentKey, TChildKey childKey, TEntity entity)
		{
			var parentCacheKey = GetParentKey(parentKey);
			var childCacheKey = GetChildKey(childKey);

			_adapter.SetField(parentCacheKey, childCacheKey, entity);
		}

		public void RemoveChild(TParentKey parentKey, TChildKey childKey)
		{
			var parentCacheKey = GetParentKey(parentKey);
			var childCacheKey = GetChildKey(childKey);

			_adapter.RemoveField(parentCacheKey, childCacheKey);
		}

		private void TouchParent(TParentKey key)
		{
			var lifetime = _lifetimeProvider.GetCachingExpiration(key);

			if (lifetime.ExtendExpirationOnReads)
			{
				var hashsetKey = GetParentKey(key);

				_expirationUpdater.UpdateLifetime(hashsetKey, lifetime.Expiration);
			}
		}

		private RedisKey GetParentKey(TParentKey key)
		{
			var uniqueKey = _uniqueParentKeyFactory(key);

			return $"{_keySpace}|{uniqueKey}";
		}

		private RedisValue GetChildKey(TChildKey key)
		{
			var uniqueKey = _uniqueChildKeyFactory(key);

			return $"{_childKeyPrefix}|{uniqueKey}";
		}
	}
}
