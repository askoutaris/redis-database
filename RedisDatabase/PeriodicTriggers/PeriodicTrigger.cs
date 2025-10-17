namespace RedisDatabase.PeriodicTriggers
{
	/// <summary>
	/// Delegate for asynchronous event handlers with no parameters.
	/// </summary>
	public delegate Task AsyncEventHandler();

	/// <summary>
	/// Defines a periodic trigger that invokes subscribed handlers at regular intervals.
	/// Supports idempotent start/stop operations and proper resource cleanup.
	/// </summary>
	public interface IPeriodicTrigger
	{
		/// <summary>
		/// Event raised periodically at the configured interval.
		/// Subscribers are invoked sequentially; exceptions in handlers are logged but don't stop execution.
		/// </summary>
		event AsyncEventHandler? OnTrigger;

		/// <summary>
		/// Starts the periodic trigger. Idempotent - subsequent calls are ignored if already running.
		/// </summary>
		/// <param name="ct">Cancellation token to stop the periodic execution.</param>
		void Start(CancellationToken ct);

		/// <summary>
		/// Stops the periodic trigger and disposes the internal timer. Idempotent - safe to call multiple times.
		/// Returns a task representing the running trigger execution, or a completed task if not started.
		/// </summary>
		/// <returns>Task that completes when the trigger has stopped.</returns>
		Task Stop();
	}

	class PeriodicTrigger : IPeriodicTrigger, IDisposable
	{
		private readonly object _sync = new();
		private readonly ILogger _logger;
		private readonly PeriodicTimer _timer;
		private Task? _task;
		private bool _isRunning = false;

		public event AsyncEventHandler? OnTrigger;

		public PeriodicTrigger(TimeSpan interval, ILogger logger)
		{
			_logger = logger;
			_timer = new PeriodicTimer(interval);
		}

		public void Start(CancellationToken ct)
		{
			lock (_sync)
			{
				if (_isRunning)
					return;

				_task = Task.Run(async () =>
				{
					try
					{
						while (await _timer.WaitForNextTickAsync(ct))
						{
							try
							{
								if (OnTrigger is not null)
									await OnTrigger.Invoke();
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, "Message: {message}", ex.Message);
							}
						}
					}
					catch (OperationCanceledException)
					{
					}
				}, ct);

				_isRunning = true;
			}
		}

		public async Task Stop()
		{
			lock (_sync)
			{
				if (_isRunning)
				{
					_isRunning = false;

					_timer.Dispose();
				}
			}

			await (_task ?? Task.CompletedTask);
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			_timer.Dispose();
		}
	}
}
