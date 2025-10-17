using StackExchange.Redis;

namespace RedisDatabase.ExpirationUpdaters
{
	/// <summary>
	/// Defines a strategy for updating Redis key expiration times, enabling flexible lifetime management patterns.
	/// </summary>
	public interface IExpirationUpdater
	{
		/// <summary>Queues a command to update the expiration time for a Redis key.</summary>
		/// <param name="key">The Redis key to update.</param>
		/// <param name="expiration">The new expiration time, or null to remove expiration.</param>
		void UpdateLifetime(RedisKey key, TimeSpan? expiration);
	}
}
