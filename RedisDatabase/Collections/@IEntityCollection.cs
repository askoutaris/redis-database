namespace RedisDatabase.Collections
{
	/// <summary>
	/// Provides entity caching operations using Redis strings with automatic key generation and expiration management.
	/// </summary>
	/// <typeparam name="TKey">The entity key type.</typeparam>
	/// <typeparam name="TEntity">The entity type to cache.</typeparam>
	public interface IEntityCollection<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Immediately retrieves an entity by key without batching and optionally extends expiration.
		/// </summary>
		/// <param name="key">The entity key.</param>
		/// <returns>A task containing the entity, or null if not found.</returns>
		Task<TEntity?> TryGet(TKey key);

		/// <summary>Queues a batched read operation to retrieve an entity by key.</summary>
		ReadResult<TEntity> TryRead(TKey key);

		/// <summary>Queues a command to store an entity with automatic key generation and expiration.</summary>
		void Set(TKey key, TEntity entity);

		/// <summary>Queues a command to delete an entity by key.</summary>
		void Remove(TKey key);
	}
}
