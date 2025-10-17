namespace RedisDatabase.LifetimeProvider
{
	/// <summary>
	/// Provides key-specific expiration policies for cached entities, enabling flexible lifetime strategies.
	/// </summary>
	public interface ILifetimeProvider
	{
		/// <summary>Determines the caching lifetime policy for a specific key.</summary>
		/// <typeparam name="TKey">The key type.</typeparam>
		/// <param name="key">The entity key to get expiration policy for.</param>
		/// <returns>A <see cref="CachingLifetime"/> containing expiration and extension settings.</returns>
		CachingLifetime GetCachingExpiration<TKey>(TKey key);
	}
}
