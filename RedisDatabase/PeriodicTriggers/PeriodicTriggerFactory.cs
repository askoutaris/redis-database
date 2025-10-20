namespace RedisDatabase.PeriodicTriggers
{
	/// <summary>
	/// Factory for creating <see cref="IPeriodicTrigger"/> instances with automatic lifecycle management.
	/// Handles logger creation and integrates with application lifetime for graceful shutdown.
	/// </summary>
	public interface IPeriodicTriggerFactory
	{
		/// <summary>
		/// Creates a periodic trigger with the specified interval and logger.
		/// </summary>
		/// <param name="interval">Time between trigger invocations.</param>
		/// <param name="logger">Logger instance for the trigger.</param>
		/// <param name="autoStart">If true, starts the trigger immediately; otherwise requires manual Start() call.</param>
		/// <returns>A configured <see cref="IPeriodicTrigger"/> instance.</returns>
		IPeriodicTrigger Create(TimeSpan interval, ILogger logger, bool autoStart = true);

		/// <summary>
		/// Creates a periodic trigger with the specified interval, logger, and custom cancellation token.
		/// </summary>
		/// <param name="interval">Time between trigger invocations.</param>
		/// <param name="logger">Logger instance for the trigger.</param>
		/// <param name="cancellationToken">Token to signal cancellation; does not use application lifetime token.</param>
		/// <param name="autoStart">If true, starts the trigger immediately; otherwise requires manual Start() call.</param>
		/// <returns>A configured <see cref="IPeriodicTrigger"/> instance.</returns>
		IPeriodicTrigger Create(TimeSpan interval, ILogger logger, CancellationToken cancellationToken, bool autoStart = true);

		/// <summary>
		/// Creates a periodic trigger with the specified interval and named logger.
		/// </summary>
		/// <param name="interval">Time between trigger invocations.</param>
		/// <param name="name">Logger category name.</param>
		/// <param name="autoStart">If true, starts the trigger immediately; otherwise requires manual Start() call.</param>
		/// <returns>A configured <see cref="IPeriodicTrigger"/> instance.</returns>
		IPeriodicTrigger Create(TimeSpan interval, string name, bool autoStart = true);

		/// <summary>
		/// Creates a periodic trigger with the specified interval, named logger, and custom cancellation token.
		/// </summary>
		/// <param name="interval">Time between trigger invocations.</param>
		/// <param name="name">Logger category name.</param>
		/// <param name="cancellationToken">Token to signal cancellation; does not use application lifetime token.</param>
		/// <param name="autoStart">If true, starts the trigger immediately; otherwise requires manual Start() call.</param>
		/// <returns>A configured <see cref="IPeriodicTrigger"/> instance.</returns>
		IPeriodicTrigger Create(TimeSpan interval, string name, CancellationToken cancellationToken, bool autoStart = true);

		/// <summary>
		/// Creates a periodic trigger with the specified interval and typed logger.
		/// </summary>
		/// <typeparam name="T">Type used for logger category name.</typeparam>
		/// <param name="interval">Time between trigger invocations.</param>
		/// <param name="autoStart">If true, starts the trigger immediately; otherwise requires manual Start() call.</param>
		/// <returns>A configured <see cref="IPeriodicTrigger"/> instance.</returns>
		IPeriodicTrigger Create<T>(TimeSpan interval, bool autoStart = true);
	}

	/// <summary>
	/// Factory implementation for creating periodic triggers with logger integration.
	/// </summary>
	public class PeriodicTriggerFactory : IPeriodicTriggerFactory
	{
		private readonly ILoggerFactory _loggerFactory;

		/// <summary>
		/// Initializes a new instance of the PeriodicTriggerFactory with the specified logger factory.
		/// </summary>
		/// <param name="loggerFactory">Factory for creating loggers.</param>
		public PeriodicTriggerFactory(ILoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory;
		}

		/// <inheritdoc/>
		public IPeriodicTrigger Create(TimeSpan interval, ILogger logger, bool autoStart = true)
		{
			return Create(interval, logger, CancellationToken.None, autoStart);
		}

		/// <inheritdoc/>
		public IPeriodicTrigger Create(TimeSpan interval, string name, bool autoStart = true)
		{
			ArgumentNullException.ThrowIfNull(name, nameof(name));

			var logger = _loggerFactory.CreateLogger(name);

			return Create(interval, logger, CancellationToken.None, autoStart);
		}

		/// <inheritdoc/>
		public IPeriodicTrigger Create(TimeSpan interval, string name, CancellationToken cancellationToken, bool autoStart = true)
		{
			ArgumentNullException.ThrowIfNull(name, nameof(name));

			var logger = _loggerFactory.CreateLogger(name);

			return Create(interval, logger, cancellationToken, autoStart);
		}

		/// <inheritdoc/>
		public IPeriodicTrigger Create<T>(TimeSpan interval, bool autoStart = true)
		{
			var logger = _loggerFactory.CreateLogger<ILogger<T>>();

			return Create(interval, logger, CancellationToken.None, autoStart);
		}

		/// <inheritdoc/>
		public IPeriodicTrigger Create(TimeSpan interval, ILogger logger, CancellationToken cancellationToken, bool autoStart = true)
		{
			if (interval <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero");

			var trigger = new PeriodicTrigger(interval, logger);

			if (autoStart)
				trigger.Start(cancellationToken);

			return trigger;
		}
	}
}
