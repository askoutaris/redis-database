namespace RedisDatabase.LifetimeProvider
{
	class DefaultLifetimeProvider : ILifetimeProvider
	{
		private readonly CachingLifetime _lifetime;

		public DefaultLifetimeProvider(TimeSpan? expiration, bool extendExpirationOnReads)
		{
			if (expiration.HasValue && expiration.Value <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero when specified");

			_lifetime = new CachingLifetime(expiration, extendExpirationOnReads);
		}

		public CachingLifetime GetCachingExpiration<TKey>(TKey key)
			=> _lifetime;
	}
}
