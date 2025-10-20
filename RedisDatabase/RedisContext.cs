using RedisDatabase.Exceptions;
using StackExchange.Redis;

namespace RedisDatabase
{
	/// <summary>
	/// Orchestrates Redis operations with support for deferred execution, batching, transactions, and conditional operations.
	/// Each context instance is single-use only and cannot be reused after commit or batch execution.
	/// </summary>
	public interface IRedisContext
	{
		/// <summary>Gets the underlying Redis database instance for direct operations.</summary>
		IDatabase Database { get; }

		/// <summary>
		/// Registers a batched operation and returns its task for later execution.
		/// All batched operations execute in parallel when <see cref="ExecuteBatch"/> is called.
		/// </summary>
		/// <typeparam name="T">The return type of the batch operation.</typeparam>
		/// <param name="action">The batch operation to register.</param>
		/// <returns>A task representing the batched operation result.</returns>
		Task<T> AddBatch<T>(Func<IBatch, Task<T>> action);

		/// <summary>
		/// Registers a deferred command for later execution during commit.
		/// Commands are executed individually or transactionally based on conditions.
		/// </summary>
		/// <param name="action">The command to register for deferred execution.</param>
		void AddCommand(Func<IDatabaseAsync, Task> action);

		/// <summary>
		/// Adds a Redis condition for transactional execution (e.g., KeyExists, HashEqual).
		/// When conditions are present, all commands execute within a transaction.
		/// </summary>
		/// <param name="condition">The condition to add to the transaction.</param>
		void AddCondition(Condition condition);

		/// <summary>
		/// Executes all registered commands either individually (if one command and no conditions) or transactionally.
		/// Throws <see cref="RedisConflictException"/> if transaction conditions fail.
		/// </summary>
		Task Commit();

		/// <summary>
		/// Executes all batched operations in parallel and waits for their completion.
		/// </summary>
		Task ExecuteBatch();
	}

	/// <summary>
	/// Implementation of Redis context for managing batched and transactional operations.
	/// </summary>
	public class RedisContext : IRedisContext
	{
		private int _used;
		private readonly List<Task> _batchTasks;
		private readonly List<Func<IDatabaseAsync, Task>> _actions;
		private readonly List<Condition> _conditions;
		private readonly IDatabase _db;
		private IBatch? _batch;

		/// <inheritdoc/>
		public IDatabase Database => _db;

		/// <summary>
		/// Gets or creates the batch instance for batched operations.
		/// </summary>
		public IBatch Batch => _batch ??= _db.CreateBatch();

		/// <summary>
		/// Initializes a new instance of the RedisContext with the specified database.
		/// </summary>
		/// <param name="db">The Redis database instance.</param>
		public RedisContext(IDatabase db)
		{
			_used = 0;
			_db = db ?? throw new ArgumentNullException(nameof(db));
			_actions = [];
			_conditions = [];
			_batchTasks = [];
		}

		/// <inheritdoc/>
		public Task<T> AddBatch<T>(Func<IBatch, Task<T>> action)
		{
			var task = action(Batch);

			_batchTasks.Add(task);

			return task;
		}

		/// <inheritdoc/>
		public void AddCommand(Func<IDatabaseAsync, Task> action)
			=> _actions.Add(action);

		/// <inheritdoc/>
		public void AddCondition(Condition condition)
			=> _conditions.Add(condition);

		/// <inheritdoc/>
		public async Task Commit()
		{
			EnsureNotUsed();

			if (_actions.Count > 1 || _conditions.Count > 0)
				await CommitTransactional();
			else
				await CommitIndividually();
		}

		/// <inheritdoc/>
		public async Task ExecuteBatch()
		{
			EnsureNotUsed();

			try
			{
				Batch.Execute();

				await Task.WhenAll(_batchTasks);
			}
			catch (Exception ex)
			{
				throw new RedisStackExchangeException(ex.Message, ex);
			}
		}

		private async Task CommitIndividually()
		{
			try
			{
				foreach (var action in _actions)
					await action(_db);
			}
			catch (Exception ex)
			{
				throw new RedisStackExchangeException(ex.Message, ex);
			}
		}

		private async Task CommitTransactional()
		{
			var transaction = _db.CreateTransaction();

			foreach (var condition in _conditions)
				transaction.AddCondition(condition);

			// DO NOT await commands in redis transaction
			// Commands executed inside a transaction do not return results until after you execute the transaction. This is simply a feature of how transactions work in Redis. At the moment you are awaiting something that hasn't even been sent yet (transactions are buffered locally until executed) - but even if it had been sent: results simply aren't available until the transaction completes.
			// https://stackoverflow.com/questions/25976231/stackexchange-redis-transaction-methods-freezes
			foreach (var action in _actions)
				_ = action(transaction);

			bool committed;

			try
			{
				committed = await transaction.ExecuteAsync();
			}
			catch (Exception ex)
			{
				throw new RedisStackExchangeException(ex.Message, ex);
			}

			if (!committed)
				throw new RedisConflictException($"Redis transaction failed - VersionConflict");
		}

		private void EnsureNotUsed()
		{
			var uses = Interlocked.Add(ref _used, 1);

			if (uses > 1)
				throw new InvalidOperationException("RedisContext can be used for one operation only - consider using a new one");
		}
	}
}
