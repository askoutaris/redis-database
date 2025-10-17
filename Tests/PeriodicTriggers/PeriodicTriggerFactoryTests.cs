using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase.PeriodicTriggers;

namespace Tests.PeriodicTriggers
{
	public class PeriodicTriggerFactoryTests
	{
		private readonly ILoggerFactory _mockLoggerFactory;
		private readonly ILogger _mockLogger;
		private readonly PeriodicTriggerFactory _factory;

		public PeriodicTriggerFactoryTests()
		{
			_mockLoggerFactory = Substitute.For<ILoggerFactory>();
			_mockLogger = Substitute.For<ILogger>();

			_mockLoggerFactory.CreateLogger(Arg.Any<string>()).Returns(_mockLogger);
			_mockLoggerFactory.CreateLogger<ILogger<object>>().Returns(_mockLogger);

			_factory = new PeriodicTriggerFactory(_mockLoggerFactory);
		}

		[Fact]
		public void Constructor_WithValidParameters_ShouldCreateInstance()
		{
			// Arrange & Act
			var factory = new PeriodicTriggerFactory(_mockLoggerFactory);

			// Assert
			Assert.NotNull(factory);
			Assert.IsType<IPeriodicTriggerFactory>(factory, exactMatch: false);
		}

		[Fact]
		public void Create_WithIntervalAndLogger_ShouldReturnStartedTrigger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);

			// Act
			var trigger = _factory.Create(interval, _mockLogger);

			// Assert
			Assert.NotNull(trigger);
			Assert.IsType<IPeriodicTrigger>(trigger, exactMatch: false);
		}

		[Fact]
		public void Create_WithIntervalLoggerAndCancellationToken_ShouldReturnStartedTrigger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			using var cts = new CancellationTokenSource();

			// Act
			var trigger = _factory.Create(interval, _mockLogger, cts.Token);

			// Assert
			Assert.NotNull(trigger);
			Assert.IsType<IPeriodicTrigger>(trigger, exactMatch: false);
		}

		[Fact]
		public async Task Create_WithIntervalLoggerAndCancellationToken_ShouldUseProvidedTokenAsync()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			var triggerInvoked = false;
			var tcs = new TaskCompletionSource<bool>();
			using var cts = new CancellationTokenSource();

			// Act
			var trigger = _factory.Create(interval, _mockLogger, cts.Token);
			trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				triggerInvoked = true;
				tcs.SetResult(true);
			};

			// Cancel the provided token
			cts.Cancel();

			// Wait a bit to ensure trigger doesn't invoke
			var completed = await Task.WhenAny(tcs.Task, Task.Delay(200));

			// Assert
			Assert.False(triggerInvoked, "Trigger should not invoke when provided token is already cancelled");
		}

		[Fact]
		public void Create_WithIntervalAndName_ShouldReturnStartedTrigger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			var name = "TestTrigger";

			// Act
			var trigger = _factory.Create(interval, name);

			// Assert
			Assert.NotNull(trigger);
			Assert.IsType<IPeriodicTrigger>(trigger, exactMatch: false);
			_mockLoggerFactory.Received(1).CreateLogger(name);
		}

		[Fact]
		public void Create_WithIntervalAndName_ShouldCreateLoggerWithProvidedName()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			var name = "MyCustomTrigger";

			// Act
			_factory.Create(interval, name);

			// Assert
			_mockLoggerFactory.Received(1).CreateLogger(name);
		}

		[Fact]
		public void Create_WithIntervalNameAndCancellationToken_ShouldReturnStartedTrigger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			var name = "TestTrigger";
			using var cts = new CancellationTokenSource();

			// Act
			var trigger = _factory.Create(interval, name, cts.Token);

			// Assert
			Assert.NotNull(trigger);
			Assert.IsType<IPeriodicTrigger>(trigger, exactMatch: false);
			_mockLoggerFactory.Received(1).CreateLogger(name);
		}

		[Fact]
		public async Task Create_WithIntervalNameAndCancellationToken_ShouldUseProvidedTokenAsync()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			var name = "TestTrigger";
			var triggerInvoked = false;
			var tcs = new TaskCompletionSource<bool>();
			using var cts = new CancellationTokenSource();

			// Act
			var trigger = _factory.Create(interval, name, cts.Token);
			trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				triggerInvoked = true;
				tcs.SetResult(true);
			};

			// Cancel the provided token
			cts.Cancel();

			// Wait a bit to ensure trigger doesn't invoke
			var completed = await Task.WhenAny(tcs.Task, Task.Delay(200));

			// Assert
			Assert.False(triggerInvoked, "Trigger should not invoke when provided token is already cancelled");
		}

		[Fact]
		public void Create_Generic_ShouldReturnStartedTrigger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);

			// Act
			var trigger = _factory.Create<PeriodicTriggerFactoryTests>(interval);

			// Assert
			Assert.NotNull(trigger);
			Assert.IsType<IPeriodicTrigger>(trigger, exactMatch: false);
		}

		[Fact]
		public void Create_Generic_ShouldCreateGenericLogger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);

			// Act
			_factory.Create<PeriodicTriggerFactoryTests>(interval);

			// Assert
			_mockLoggerFactory.Received(1).CreateLogger<ILogger<PeriodicTriggerFactoryTests>>();
		}

		[Fact]
		public async Task Create_WithAllOverloads_ShouldProduceWorkingTriggers()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var name = "TestTrigger";
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			var results = new bool[5];

			// Act - Create triggers with different overloads
			var trigger1 = _factory.Create(interval, _mockLogger);
			var trigger2 = _factory.Create(interval, _mockLogger, cts.Token);
			var trigger3 = _factory.Create(interval, name);
			var trigger4 = _factory.Create(interval, name, cts.Token);
			var trigger5 = _factory.Create<PeriodicTriggerFactoryTests>(interval);

			// Subscribe to all triggers
			trigger1.OnTrigger += async () => { await Task.Delay(1); results[0] = true; };
			trigger2.OnTrigger += async () => { await Task.Delay(1); results[1] = true; };
			trigger3.OnTrigger += async () => { await Task.Delay(1); results[2] = true; };
			trigger4.OnTrigger += async () => { await Task.Delay(1); results[3] = true; };
			trigger5.OnTrigger += async () => { await Task.Delay(1); results[4] = true; };

			// Wait for triggers to fire
			await Task.Delay(150);

			// Assert
			Assert.True(results[0], "Trigger 1 should have fired");
			Assert.True(results[1], "Trigger 2 should have fired");
			Assert.True(results[2], "Trigger 3 should have fired");
			Assert.True(results[3], "Trigger 4 should have fired");
			Assert.True(results[4], "Trigger 5 should have fired");

			// Cleanup
			await trigger1.Stop();
			await trigger2.Stop();
			await trigger3.Stop();
			await trigger4.Stop();
			await trigger5.Stop();
		}

		[Fact]
		public void Create_WithNullLogger_ShouldCreateTrigger()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);

			// Act
			var trigger = _factory.Create(interval: interval, logger: null!);

			// Assert
			Assert.NotNull(trigger);
		}

		[Fact]
		public void Create_WithNullName_ShouldThrowArgumentNullException()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);

			// Act & Assert
			var exception = Assert.Throws<ArgumentNullException>(() => _factory.Create(interval, name: null!));
			Assert.Equal("name", exception.ParamName);
		}

		[Fact]
		public void Create_WithNullNameAndCancellationToken_ShouldThrowArgumentNullException()
		{
			// Arrange
			var interval = TimeSpan.FromSeconds(1);
			using var cts = new CancellationTokenSource();

			// Act & Assert
			var exception = Assert.Throws<ArgumentNullException>(() => _factory.Create(interval, name: null!, cts.Token));
			Assert.Equal("name", exception.ParamName);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(-100)]
		public void Create_WithZeroOrNegativeInterval_ShouldThrowArgumentOutOfRangeException(int intervalMs)
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(intervalMs);

			// Act & Assert
			var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(interval, _mockLogger));
			Assert.Equal("interval", exception.ParamName);
			Assert.Contains("must be greater than zero", exception.Message, StringComparison.OrdinalIgnoreCase);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		public void Create_WithZeroOrNegativeInterval_AllOverloads_ShouldThrowArgumentOutOfRangeException(int intervalMs)
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(intervalMs);
			var name = "TestTrigger";
			using var cts = new CancellationTokenSource();

			// Act & Assert - Test all overloads
			Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(interval, _mockLogger));
			Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(interval, _mockLogger, cts.Token));
			Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(interval, name));
			Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(interval, name, cts.Token));
			Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create<PeriodicTriggerFactoryTests>(interval));
		}

		[Theory]
		[InlineData(1)]
		[InlineData(10)]
		[InlineData(100)]
		[InlineData(1000)]
		public void Create_WithVariousIntervals_ShouldCreateTriggers(int intervalMs)
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(intervalMs);

			// Act
			var trigger = _factory.Create(interval, _mockLogger);

			// Assert
			Assert.NotNull(trigger);
			Assert.IsType<IPeriodicTrigger>(trigger, exactMatch: false);
		}

		[Fact]
		public async Task Create_WithAutoStartFalse_ShouldNotStartAutomatically()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var triggerInvoked = false;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			var trigger = _factory.Create(interval, _mockLogger, cts.Token, autoStart: false);
			trigger.OnTrigger += async () => { await Task.Delay(1); triggerInvoked = true; };

			// Wait to ensure trigger doesn't fire
			await Task.Delay(150);

			// Assert
			Assert.False(triggerInvoked, "Trigger should not invoke when autoStart is false");

			// Cleanup
			await trigger.Stop();
		}

		[Fact]
		public async Task Create_WithAutoStartFalse_ThenManualStart_ShouldStartWorking()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var triggerInvoked = false;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			var trigger = _factory.Create(interval, _mockLogger, cts.Token, autoStart: false);
			trigger.OnTrigger += async () => { await Task.Delay(1); triggerInvoked = true; };

			// Verify it doesn't fire initially
			await Task.Delay(100);
			Assert.False(triggerInvoked, "Trigger should not invoke before manual start");

			// Now start manually
			trigger.Start(cts.Token);
			await Task.Delay(150);

			// Assert
			Assert.True(triggerInvoked, "Trigger should invoke after manual start");

			// Cleanup
			await trigger.Stop();
		}

		[Fact]
		public async Task Create_WithAutoStartTrue_ShouldStartAutomatically()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var triggerInvoked = false;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			var trigger = _factory.Create(interval, _mockLogger, cts.Token, autoStart: true);
			trigger.OnTrigger += async () => { await Task.Delay(1); triggerInvoked = true; };

			// Wait for trigger to fire
			await Task.Delay(150);

			// Assert
			Assert.True(triggerInvoked, "Trigger should invoke when autoStart is true");

			// Cleanup
			await trigger.Stop();
		}

		[Fact]
		public async Task Create_WithNameAndAutoStartFalse_ShouldNotStartAutomatically()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var name = "TestTrigger";
			var triggerInvoked = false;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			var trigger = _factory.Create(interval, name, cts.Token, autoStart: false);
			trigger.OnTrigger += async () => { await Task.Delay(1); triggerInvoked = true; };

			// Wait to ensure trigger doesn't fire
			await Task.Delay(150);

			// Assert
			Assert.False(triggerInvoked, "Trigger should not invoke when autoStart is false");

			// Cleanup
			await trigger.Stop();
		}

		[Fact]
		public async Task Create_GenericWithAutoStartFalse_ShouldNotStartAutomatically()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var triggerInvoked = false;

			// Act
			var trigger = _factory.Create<PeriodicTriggerFactoryTests>(interval, autoStart: false);
			trigger.OnTrigger += async () => { await Task.Delay(1); triggerInvoked = true; };

			// Wait to ensure trigger doesn't fire
			await Task.Delay(150);

			// Assert
			Assert.False(triggerInvoked, "Trigger should not invoke when autoStart is false");

			// Cleanup
			await trigger.Stop();
		}

		[Fact]
		public async Task Create_AllOverloadsWithAutoStartFalse_ShouldNotStartAutomatically()
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(50);
			var name = "TestTrigger";
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			var results = new bool[5];

			// Act - Create triggers with different overloads, all with autoStart: false
			var trigger1 = _factory.Create(interval, _mockLogger, autoStart: false);
			var trigger2 = _factory.Create(interval, _mockLogger, cts.Token, autoStart: false);
			var trigger3 = _factory.Create(interval, name, autoStart: false);
			var trigger4 = _factory.Create(interval, name, cts.Token, autoStart: false);
			var trigger5 = _factory.Create<PeriodicTriggerFactoryTests>(interval, autoStart: false);

			// Subscribe to all triggers
			trigger1.OnTrigger += async () => { await Task.Delay(1); results[0] = true; };
			trigger2.OnTrigger += async () => { await Task.Delay(1); results[1] = true; };
			trigger3.OnTrigger += async () => { await Task.Delay(1); results[2] = true; };
			trigger4.OnTrigger += async () => { await Task.Delay(1); results[3] = true; };
			trigger5.OnTrigger += async () => { await Task.Delay(1); results[4] = true; };

			// Wait to ensure triggers don't fire
			await Task.Delay(150);

			// Assert
			Assert.False(results[0], "Trigger 1 should not have fired");
			Assert.False(results[1], "Trigger 2 should not have fired");
			Assert.False(results[2], "Trigger 3 should not have fired");
			Assert.False(results[3], "Trigger 4 should not have fired");
			Assert.False(results[4], "Trigger 5 should not have fired");

			// Cleanup - Stop should return completed task for triggers that were never started
			var stopTask1 = trigger1.Stop();
			var stopTask2 = trigger2.Stop();
			var stopTask3 = trigger3.Stop();
			var stopTask4 = trigger4.Stop();
			var stopTask5 = trigger5.Stop();

			Assert.True(stopTask1.IsCompleted, "Stop should return completed task when never started");
			Assert.True(stopTask2.IsCompleted, "Stop should return completed task when never started");
			Assert.True(stopTask3.IsCompleted, "Stop should return completed task when never started");
			Assert.True(stopTask4.IsCompleted, "Stop should return completed task when never started");
			Assert.True(stopTask5.IsCompleted, "Stop should return completed task when never started");

			await stopTask1;
			await stopTask2;
			await stopTask3;
			await stopTask4;
			await stopTask5;
		}

		[Fact]
		public async Task Create_WithAutoStartFalse_StopShouldReturnCompletedTask()
		{
			// This test explicitly verifies the coverage of PeriodicTrigger.Stop() line 88:
			// return _task ?? Task.CompletedTask; when _isRunning is false and _task is null
			// Arrange
			var interval = TimeSpan.FromSeconds(1);

			// Act
			var trigger = _factory.Create(interval, _mockLogger, autoStart: false);
			var stopTask = trigger.Stop();

			// Assert - Should return Task.CompletedTask since trigger was never started
			Assert.NotNull(stopTask);
			Assert.True(stopTask.IsCompleted);
			Assert.True(stopTask == Task.CompletedTask || stopTask.IsCompletedSuccessfully);
			await stopTask; // Should not throw
		}
	}
}
