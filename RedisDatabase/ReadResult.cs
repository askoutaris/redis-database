namespace RedisDatabase
{
	/// <summary>
	/// Represents a deferred read result that wraps an asynchronous task for batch execution.
	/// Access to Value requires the batch to be executed via RedisContext.ExecuteBatch().
	/// </summary>
	/// <typeparam name="T">The type of the value being read.</typeparam>
	public readonly struct ReadResult<T>
	{
		private readonly Task<T?> _task;

		/// <summary>
		/// Gets the read value if the batch has been executed, otherwise throws an exception.
		/// </summary>
		/// <exception cref="Exception">Thrown when accessing Value before batch execution.</exception>
		public T? Value => _task.IsCompleted ? _task.Result : throw new Exception($"Read not performed yet - consider calling RedisContext.ExecuteBatch()");

		/// <summary>
		/// Initializes a new ReadResult with the specified task.
		/// </summary>
		/// <param name="task">The asynchronous task representing the deferred read operation.</param>
		public ReadResult(Task<T?> task)
		{
			_task = task;
		}
	}
}
