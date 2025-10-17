namespace RedisDatabase
{
	public readonly struct ReadResult<T>
	{
		private readonly Task<T?> _task;

		public T? Value => _task.IsCompleted ? _task.Result : throw new Exception($"Read not performed yet - consider calling RedisContext.ExecuteBatch()");

		public ReadResult(Task<T?> task)
		{
			_task = task;
		}
	}
}
