using System.Reflection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase.PeriodicTriggers;

namespace Tests.PeriodicTriggers
{
	public class PeriodicTriggerTests : IDisposable
	{
		private readonly ILogger _mockLogger;
		private readonly PeriodicTrigger _trigger;
		private readonly TimeSpan _testInterval = TimeSpan.FromMilliseconds(50);

		public PeriodicTriggerTests()
		{
			_mockLogger = Substitute.For<ILogger>();
			_trigger = new PeriodicTrigger(_testInterval, _mockLogger);
		}

		public void Dispose()
		{
			_trigger.Dispose();
		}

		[Fact]
		public void Constructor_WithValidParameters_ShouldCreateInstance()
		{
			// Arrange & Act
			var periodicWork = new PeriodicTrigger(TimeSpan.FromSeconds(1), _mockLogger);

			// Assert
			Assert.NotNull(periodicWork);
			Assert.IsAssignableFrom<IPeriodicTrigger>(periodicWork);
		}

		[Fact]
		public void Constructor_WithNullLogger_ShouldStillCreateInstance()
		{
			// This tests the actual behavior - constructor doesn't validate parameters
			// Arrange & Act
			var periodicWork = new PeriodicTrigger(TimeSpan.FromSeconds(1), null!);

			// Assert
			Assert.NotNull(periodicWork);
		}

		[Fact]
		public async Task OnTick_WhenSubscribed_ShouldBeInvokedPeriodically()
		{
			// Arrange
			var tickCount = 0;
			var tickTcs = new TaskCompletionSource<bool>();

			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1); // Simulate some work
				tickCount++;
				if (tickCount >= 3)
					tickTcs.SetResult(true);
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			_trigger.Start(cts.Token);

			// Wait for at least 3 ticks or timeout
			var completed = await Task.WhenAny(tickTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
			await _trigger.Stop();

			// Assert
			Assert.True(tickCount >= 3, $"Expected at least 3 ticks, but got {tickCount}");
		}

		[Fact]
		public async Task OnTick_WithMultipleSubscribers_ShouldInvokeAllSubscribers()
		{
			// Arrange
			var subscriber1Called = false;
			var subscriber2Called = false;
			var tcs = new TaskCompletionSource<bool>();

			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				subscriber1Called = true;
				if (subscriber1Called && subscriber2Called)
					tcs.SetResult(true);
			};

			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				subscriber2Called = true;
				if (subscriber1Called && subscriber2Called)
					tcs.SetResult(true);
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			_trigger.Start(cts.Token);
			await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
			await _trigger.Stop();

			// Assert
			Assert.True(subscriber1Called, "First subscriber should have been called");
			Assert.True(subscriber2Called, "Second subscriber should have been called");
		}

		[Fact]
		public async Task OnTick_WithNoSubscribers_ShouldNotThrow()
		{
			// Arrange
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

			// Act & Assert - Should not throw
			_trigger.Start(cts.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(150));
			await _trigger.Stop();
		}

		[Fact]
		public async Task OnTick_WhenSubscriberThrowsException_ShouldLogErrorAndContinue()
		{
			// Arrange
			var firstCallException = new InvalidOperationException("First call error");
			var secondCallCompleted = false;
			var callCount = 0;

			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				callCount++;
				if (callCount == 1)
					throw firstCallException;
				else if (callCount == 2)
					secondCallCompleted = true;
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

			// Act
			_trigger.Start(cts.Token);

			// Wait for at least 2 iterations
			await Task.Delay(TimeSpan.FromMilliseconds(200));
			await _trigger.Stop();

			// Assert
			Assert.True(callCount >= 2, $"Expected at least 2 calls, but got {callCount}");
			Assert.True(secondCallCompleted, "Second call should have completed successfully");
		}

		[Fact]
		public async Task Start_WithCancellationToken_ShouldStopWhenCancelled()
		{
			// Arrange
			var tickCount = 0;
			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				tickCount++;
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

			// Act
			_trigger.Start(cts.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(150));
			var initialTickCount = tickCount;

			// Wait a bit more to ensure it actually stopped
			await Task.Delay(TimeSpan.FromMilliseconds(100));
			var finalTickCount = tickCount;

			await _trigger.Stop();

			// Assert
			Assert.True(initialTickCount >= 1, "Should have ticked at least once before cancellation");
			Assert.True(finalTickCount == initialTickCount, "Should not tick after cancellation");
		}

		[Fact]
		public async Task Stop_WithoutStart_ShouldReturnCompletedTask()
		{
			// Act
			var stopTask = _trigger.Stop();

			// Assert
			Assert.True(stopTask.IsCompleted, "Stop should return completed task when not started");
			await stopTask; // Should not throw
		}

		[Fact]
		public async Task Stop_AfterStart_ShouldWaitForTaskCompletion()
		{
			// Arrange
			var tickOccurred = false;
			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				tickOccurred = true;
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act
			_trigger.Start(cts.Token);
			await Task.Delay(_testInterval * 2); // Wait for at least one tick
			var stopTask = _trigger.Stop();

			// Assert
			Assert.True(tickOccurred, "At least one tick should have occurred");
			Assert.NotNull(stopTask);
			await stopTask; // Should complete without throwing
		}

		[Fact]
		public async Task Start_CalledAfterStop_ShouldNotWorkDueToDisposedTimer()
		{
			// With the new implementation, once Stop() is called, the timer is disposed
			// and subsequent Start() calls should not work
			// Arrange
			var tickCount = 0;
			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				tickCount++;
			};

			using var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

			// Act - First start (should work)
			_trigger.Start(cts1.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(150));
			await _trigger.Stop(); // This disposes the timer

			var firstTickCount = tickCount;

			using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

			// Act - Second start (should not work due to disposed timer)
			_trigger.Start(cts2.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(150));
			await _trigger.Stop();

			// Assert
			Assert.True(firstTickCount >= 1, $"First start should have ticked at least once, but got {firstTickCount}");
			Assert.Equal(firstTickCount, tickCount); // No additional ticks should have occurred
		}

		[Theory]
		[InlineData(10)]
		[InlineData(50)]
		[InlineData(100)]
		public async Task PeriodicWork_WithDifferentIntervals_ShouldRespectTiming(int intervalMs)
		{
			// Arrange
			var interval = TimeSpan.FromMilliseconds(intervalMs);
			var periodicWork = new PeriodicTrigger(interval, _mockLogger);
			var tickTimes = new List<DateTime>();

			periodicWork.OnTrigger += async () =>
			{
				await Task.Delay(1);
				lock (tickTimes)
				{
					tickTimes.Add(DateTime.UtcNow);
				}
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(intervalMs * 5));

			// Act
			periodicWork.Start(cts.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(intervalMs * 5 + 50));
			await periodicWork.Stop();

			// Assert
			Assert.True(tickTimes.Count >= 2, $"Expected at least 2 ticks for {intervalMs}ms interval");

			// Verify timing is approximately correct (allowing for some tolerance)
			if (tickTimes.Count >= 2)
			{
				var averageInterval = tickTimes.Skip(1)
					.Zip(tickTimes, (current, previous) => (current - previous).TotalMilliseconds)
					.Average();

				// Use larger tolerance for small intervals due to timer resolution limitations
				var tolerance = Math.Max(intervalMs * 0.5, 10.0); // At least 10ms tolerance or 50% of interval
				Assert.True(averageInterval >= intervalMs - tolerance && averageInterval <= intervalMs + tolerance,
					$"Average interval {averageInterval:F1}ms should be approximately {intervalMs}ms (±{tolerance}ms)");
			}
		}

		[Fact]
		public void Dispose_ShouldDisposeTimer()
		{
			// Arrange
			var periodicWork = new PeriodicTrigger(TimeSpan.FromMilliseconds(100), _mockLogger);

			// Act - Should not throw
			periodicWork.Dispose();

			// Assert - Calling Dispose multiple times should not throw
			periodicWork.Dispose();
		}

		[Fact]
		public async Task Stop_ShouldDisposeTimerAndExitLoop()
		{
			// This test verifies that Stop() disposes the timer, causing the loop to exit normally
			// and covering the normal exit path from the while loop
			// Arrange
			var tickCount = 0;
			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				tickCount++;
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Long timeout

			// Act
			_trigger.Start(cts.Token);

			// Let it tick at least once
			await Task.Delay(TimeSpan.FromMilliseconds(100));

			// Stop should dispose the timer and cause normal loop exit
			await _trigger.Stop();

			// Assert
			Assert.True(tickCount >= 1, $"Expected at least 1 tick, but got {tickCount}");

			// Verify the task completed without cancellation
			var task = GetPrivateField<Task?>(_trigger, "_task");
			Assert.NotNull(task);
			Assert.True(task.IsCompletedSuccessfully, "Task should complete successfully when timer is disposed via Stop()");
		}

		[Fact]
		public async Task Stop_CalledMultipleTimes_ShouldBeIdempotent()
		{
			// Arrange
			_trigger.OnTrigger += async () => await Task.Delay(1);
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

			// Act
			_trigger.Start(cts.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(50));

			// Multiple stops should not cause issues
			await _trigger.Stop();
			await _trigger.Stop();
			await _trigger.Stop();

			// Assert - No exception should be thrown
		}

		[Fact]
		public async Task OnTick_WithLongRunningHandler_ShouldNotBlockSubsequentTicks()
		{
			// Arrange
			var quickTicksCount = 0;
			var longRunningStarted = false;
			var longRunningCompleted = false;

			_trigger.OnTrigger += async () =>
			{
				if (!longRunningStarted)
				{
					longRunningStarted = true;
					await Task.Delay(TimeSpan.FromMilliseconds(200)); // Long running operation
					longRunningCompleted = true;
				}
				else
				{
					await Task.Delay(1);
					quickTicksCount++;
				}
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

			// Act
			_trigger.Start(cts.Token);
			await Task.Delay(TimeSpan.FromMilliseconds(350));
			await _trigger.Stop();

			// Assert
			Assert.True(longRunningStarted, "Long running handler should have started");
			Assert.True(longRunningCompleted, "Long running handler should have completed");
			Assert.True(quickTicksCount >= 1, "Quick ticks should have occurred while long handler was running");
		}

		[Fact]
		public async Task Start_CalledMultipleTimes_ShouldBeIdempotent()
		{
			// Arrange
			var tickCount = 0;
			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				tickCount++;
			};

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

			// Act - Call Start multiple times
			_trigger.Start(cts.Token);
			_trigger.Start(cts.Token);
			_trigger.Start(cts.Token);

			await Task.Delay(TimeSpan.FromMilliseconds(150));
			await _trigger.Stop();

			// Assert - Should have only one running task, not multiple
			var task = GetPrivateField<Task?>(_trigger, "_task");
			Assert.NotNull(task);
			Assert.True(tickCount >= 1, "Should have ticked at least once");
		}

		[Fact]
		public async Task Start_CalledMultipleTimesWithDifferentTokens_ShouldUseFirstToken()
		{
			// Arrange
			var tickCount = 0;
			_trigger.OnTrigger += async () =>
			{
				await Task.Delay(1);
				tickCount++;
			};

			using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

			// Act - Start with first token (long timeout), then try with second token (short timeout)
			_trigger.Start(cts1.Token);
			_trigger.Start(cts2.Token); // Should be ignored

			// Wait longer than second token's timeout but within first token's timeout
			await Task.Delay(TimeSpan.FromMilliseconds(150));

			// Assert - Should still be running because first token is still valid
			Assert.True(tickCount >= 2, $"Should have ticked multiple times using first token, but got {tickCount}");

			await _trigger.Stop();
		}

		[Fact]
		public async Task Stop_CalledMultipleTimesWithoutStart_ShouldBeIdempotent()
		{
			// Act - Call Stop multiple times without ever starting
			var stopTask1 = _trigger.Stop();
			var stopTask2 = _trigger.Stop();
			var stopTask3 = _trigger.Stop();

			// Assert - All should return completed tasks without throwing
			Assert.True(stopTask1.IsCompleted);
			Assert.True(stopTask2.IsCompleted);
			Assert.True(stopTask3.IsCompleted);

			await stopTask1;
			await stopTask2;
			await stopTask3;
		}

		private static T GetPrivateField<T>(object obj, string fieldName)
		{
			var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			return (T)field!.GetValue(obj)!;
		}
	}
}
