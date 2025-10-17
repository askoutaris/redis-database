namespace RedisDatabase.Collections
{
	/// <summary>
	/// Provides parent-child entity caching operations using Redis hashes for hierarchical data structures.
	/// </summary>
	/// <typeparam name="TParentKey">The parent entity key type.</typeparam>
	/// <typeparam name="TChildKey">The child entity key type.</typeparam>
	/// <typeparam name="TEntity">The child entity type to cache.</typeparam>
	public interface IChildEntityCollection<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Immediately retrieves a child entity by parent and child keys without batching and optionally extends parent expiration.
		/// </summary>
		/// <param name="parentKey">The parent entity key.</param>
		/// <param name="childKey">The child entity key.</param>
		/// <returns>A task containing the child entity, or null if not found.</returns>
		Task<TEntity?> TryGetChild(TParentKey parentKey, TChildKey childKey);

		/// <summary>Queues a batched read operation to retrieve a child entity by parent and child keys.</summary>
		ReadResult<TEntity> TryReadChild(TParentKey parentKey, TChildKey childKey);

		/// <summary>Queues a command to store a child entity under a parent with automatic key generation and expiration.</summary>
		void SetChild(TParentKey parentKey, TChildKey childKey, TEntity entity);

		/// <summary>Queues a command to delete a child entity by parent and child keys.</summary>
		void RemoveChild(TParentKey parentKey, TChildKey childKey);
	}
}
