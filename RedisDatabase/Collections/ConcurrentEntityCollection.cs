using RedisDatabase.Adapters;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using StackExchange.Redis;

namespace RedisDatabase.Collections
{
	class ConcurrentEntityCollection<TKey, TEntity> : IEntityCollection<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		private readonly RedisValue _entityField = "obj";
		private readonly RedisValue _concurrencyTokenField = "ct";

		private readonly string _keySpace;
		private readonly IHashsetAdapter _adapter;
		private readonly ILifetimeProvider _lifetimeProvider;
		private readonly IExpirationUpdater _expirationUpdater;
		private readonly IRedisContext _context;
		private readonly Func<TKey, string> _uniqueKeyFactory;
		private readonly Func<TEntity, string?> _oldConcurrencyTokenSelector;
		private readonly Func<TEntity, string> _newConcurrencyTokenSelector;

		public ConcurrentEntityCollection(
			string keySpace,
			IHashsetAdapter adapter,
			ILifetimeProvider lifetimeProvider,
			IExpirationUpdater expirationUpdater,
			IRedisContext context,
			Func<TKey, string> uniqueKeyFactory,
			Func<TEntity, string?> oldConcurrencyTokenSelector,
			Func<TEntity, string> newConcurrencyTokenSelector)
		{
			_keySpace = keySpace;
			_adapter = adapter;
			_lifetimeProvider = lifetimeProvider;
			_expirationUpdater = expirationUpdater;
			_context = context;
			_uniqueKeyFactory = uniqueKeyFactory;
			_oldConcurrencyTokenSelector = oldConcurrencyTokenSelector;
			_newConcurrencyTokenSelector = newConcurrencyTokenSelector;
		}

		public async Task<TEntity?> TryGet(TKey key)
		{
			var hashsetKey = GetHashsetKey(key);

			var entity = await _adapter.TryGetField<TEntity>(hashsetKey, _entityField);

			Touch(key);

			return entity;
		}

		public ReadResult<TEntity> TryRead(TKey key)
		{
			var hashsetKey = GetHashsetKey(key);

			var read = _adapter.TryReadField<TEntity>(hashsetKey, _entityField);

			Touch(key);

			return read;
		}

		public void Set(TKey key, TEntity entity)
		{
			var hashsetKey = GetHashsetKey(key);

			SetVersion(key, entity);

			_adapter.SetField(hashsetKey, _entityField, entity);

			var lifetime = _lifetimeProvider.GetCachingExpiration(key);

			_context.AddCommand(db => db.KeyExpireAsync(
				key: hashsetKey,
				expiry: lifetime.Expiration));
		}

		public void Remove(TKey key)
		{
			var hashsetKey = GetHashsetKey(key);

			_adapter.Remove(hashsetKey);
		}

		public void Touch(TKey key)
		{
			var lifetime = _lifetimeProvider.GetCachingExpiration(key);

			if (lifetime.ExtendExpirationOnReads)
			{
				var hashsetKey = GetHashsetKey(key);

				_expirationUpdater.UpdateLifetime(hashsetKey, lifetime.Expiration);
			}
		}

		private void SetVersion(TKey key, TEntity entity)
		{
			var hashsetKey = GetHashsetKey(key);
			var oldConcurrencyToken = _oldConcurrencyTokenSelector(entity);

			var condition = oldConcurrencyToken is null
				? Condition.KeyNotExists(hashsetKey)
				: Condition.HashEqual(hashsetKey, _concurrencyTokenField, oldConcurrencyToken);

			_context.AddCondition(condition);

			var newConcurrencyToken = _newConcurrencyTokenSelector(entity);

			_context.AddCommand(db => db.HashSetAsync(
				key: hashsetKey,
				hashField: _concurrencyTokenField,
				value: newConcurrencyToken));
		}

		private RedisKey GetHashsetKey(TKey key)
		{
			var uniqueKey = _uniqueKeyFactory(key);

			return $"{_keySpace}|{uniqueKey}";
		}
	}
}
