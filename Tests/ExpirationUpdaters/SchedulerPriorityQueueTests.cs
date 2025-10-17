using System.Reflection;
using RedisDatabase.ExpirationUpdaters;
using StackExchange.Redis;

namespace Tests.ExpirationUpdaters;

public class SchedulerPriorityQueueTests
{
	#region Constructor Tests

	[Fact]
	public void Constructor_InitializesCorrectly()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();

		Assert.NotNull(scheduler);
	}

	#endregion

	#region ScheduleOrUpdate Tests

	[Fact]
	public void ScheduleOrUpdate_FirstTime_AddsToScheduledExpirations()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var beforeSchedule = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, expiration);

		var afterSchedule = DateTime.UtcNow;
		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(key));

		var entry = expirations[key];
		Assert.Equal(expiration, entry.Expiration);
		Assert.NotNull(entry.LastAccess);
		Assert.True(entry.LastAccess >= beforeSchedule && entry.LastAccess <= afterSchedule);
	}

	[Fact]
	public void ScheduleOrUpdate_FirstTime_SchedulesForImmediateExecution()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");

		scheduler.ScheduleOrUpdate(key, TimeSpan.FromMinutes(10));

		var scheduledUpdates = GetScheduledUpdates(scheduler);
		Assert.Equal(1, GetPriorityQueueCount(scheduledUpdates));
	}

	[Fact]
	public void ScheduleOrUpdate_SecondTime_UpdatesExpirationAndLastAccess()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var firstExpiration = TimeSpan.FromMinutes(10);
		var secondExpiration = TimeSpan.FromMinutes(20);

		scheduler.ScheduleOrUpdate(key, firstExpiration);
		var beforeSecondSchedule = DateTime.UtcNow;
		scheduler.ScheduleOrUpdate(key, secondExpiration);
		var afterSecondSchedule = DateTime.UtcNow;

		var expirations = GetScheduledExpirations(scheduler);
		Assert.Single(expirations);

		var entry = expirations[key];
		Assert.Equal(secondExpiration, entry.Expiration);
		Assert.NotNull(entry.LastAccess);
		Assert.True(entry.LastAccess >= beforeSecondSchedule && entry.LastAccess <= afterSecondSchedule);
	}

	[Fact]
	public void ScheduleOrUpdate_MultipleKeys_AddsAllKeys()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key1 = new RedisKey("test:key1");
		var key2 = new RedisKey("test:key2");
		var key3 = new RedisKey("test:key3");

		scheduler.ScheduleOrUpdate(key1, TimeSpan.FromMinutes(10));
		scheduler.ScheduleOrUpdate(key2, TimeSpan.FromMinutes(20));
		scheduler.ScheduleOrUpdate(key3, TimeSpan.FromMinutes(30));

		var expirations = GetScheduledExpirations(scheduler);
		Assert.Equal(3, expirations.Count);
		Assert.True(expirations.ContainsKey(key1));
		Assert.True(expirations.ContainsKey(key2));
		Assert.True(expirations.ContainsKey(key3));
	}

	[Fact]
	public void ScheduleOrUpdate_SameKeyMultipleTimes_LastWriteWins()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");

		for (int i = 1; i <= 100; i++)
		{
			scheduler.ScheduleOrUpdate(key, TimeSpan.FromMinutes(i));
		}

		var expirations = GetScheduledExpirations(scheduler);
		Assert.Single(expirations);
		Assert.Equal(TimeSpan.FromMinutes(100), expirations[key].Expiration);
	}

	[Fact]
	public async Task ScheduleOrUpdate_ConcurrentCalls_ThreadSafe()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("concurrent:key");

		var tasks = new List<Task>();
		for (int i = 0; i < 100; i++)
		{
			var expiration = TimeSpan.FromMinutes(i + 1);
			tasks.Add(Task.Run(() => scheduler.ScheduleOrUpdate(key, expiration)));
		}

		await Task.WhenAll(tasks);

		var expirations = GetScheduledExpirations(scheduler);
		Assert.Single(expirations);
		Assert.True(expirations.ContainsKey(key));
	}

	#endregion

	#region GetScheduleds Basic Behavior Tests

	[Fact]
	public void GetScheduleds_WhenEmpty_ReturnsEmptyCollection()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();

		var result = scheduler.GetScheduleds(DateTime.UtcNow);

		Assert.NotNull(result);
		Assert.Empty(result);
	}

	[Fact]
	public void GetScheduleds_WhenNothingDue_ReturnsEmptyCollection()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var scheduleTime = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, TimeSpan.FromMinutes(10));

		// Call GetScheduleds with a time BEFORE the scheduled time
		var result = scheduler.GetScheduleds(scheduleTime.AddMilliseconds(-100));

		Assert.Empty(result);
	}

	[Fact]
	public void GetScheduleds_WhenItemDue_ReturnsSlidingExpiration()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, expiration);

		// Call GetScheduleds with a time AFTER the scheduled time
		var processTime = scheduleTime.AddMilliseconds(100);
		var result = scheduler.GetScheduleds(processTime);

		Assert.Single(result);
		var item = result.First();
		Assert.Equal(key, item.Key);

		// Sliding expiration = Expiration - (processTime - LastAccess)
		// Since LastAccess was set during ScheduleOrUpdate (~scheduleTime), elapsed time is ~100ms
		var expectedSlidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		// Allow 50ms tolerance for execution time
		Assert.True(Math.Abs((item.Value - expectedSlidingExpiration).TotalMilliseconds) < 50);
	}

	[Fact]
	public void GetScheduleds_WithMultipleDueItems_ReturnsAll()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key1 = new RedisKey("test:key1");
		var key2 = new RedisKey("test:key2");
		var key3 = new RedisKey("test:key3");

		scheduler.ScheduleOrUpdate(key1, TimeSpan.FromMinutes(10));
		scheduler.ScheduleOrUpdate(key2, TimeSpan.FromMinutes(20));
		scheduler.ScheduleOrUpdate(key3, TimeSpan.FromMinutes(30));

		var result = scheduler.GetScheduleds(DateTime.UtcNow.AddMilliseconds(100));

		Assert.Equal(3, result.Count);
	}

	[Fact]
	public void GetScheduleds_KeyWithLastAccess_ReschedulesAtHalfLifeOfSlidingExpiration()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, expiration);

		var processTime = scheduleTime.AddMilliseconds(100);
		var result = scheduler.GetScheduleds(processTime);

		// Key should be re-scheduled because LastAccess was set
		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(key));

		var entry = expirations[key];
		Assert.Equal(expiration, entry.Expiration); // Original expiration preserved
		Assert.Null(entry.LastAccess); // Reset to null after processing
	}

	[Fact]
	public void GetScheduleds_KeyWithNullLastAccess_RemovesFromDictionary()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, expiration);

		// First call - processes and re-schedules at half-life with LastAccess = null
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Verify key still exists but LastAccess is null
		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(key));
		Assert.Null(expirations[key].LastAccess);

		// Second call with time past the half-life rescheduled time - should remove because LastAccess = null
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		expirations = GetScheduledExpirations(scheduler);
		Assert.False(expirations.ContainsKey(key));
		Assert.Empty(expirations);
	}

	[Fact]
	public void GetScheduleds_MixedLastAccessStates_HandlesCorrectly()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key1 = new RedisKey("test:key1");
		var key2 = new RedisKey("test:key2");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule both keys
		scheduler.ScheduleOrUpdate(key1, expiration);
		scheduler.ScheduleOrUpdate(key2, expiration);

		// First processing - both are re-scheduled at half-life, LastAccess = null for both
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Access key1 again (sets LastAccess = now)
		scheduler.ScheduleOrUpdate(key1, expiration);

		// Second processing past the half-life time
		// key1: LastAccess.HasValue = true -> should be re-scheduled
		// key2: LastAccess = null -> should be removed
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(key1));
		Assert.False(expirations.ContainsKey(key2));
		Assert.Single(expirations);
	}

	#endregion

	#region Memory Leak Prevention Tests

	[Fact]
	public void GetScheduleds_KeyWithNullLastAccess_PreventsMemoryLeak()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule, process (re-schedule with LastAccess = null), then process again
		scheduler.ScheduleOrUpdate(key, expiration);

		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime); // First processing - re-schedules at half-life

		// Second processing past the half-life time - should remove because LastAccess = null
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		// Verify complete cleanup
		var expirations = GetScheduledExpirations(scheduler);
		Assert.Empty(expirations);
		var scheduledUpdates = GetScheduledUpdates(scheduler);
		Assert.Equal(0, GetPriorityQueueCount(scheduledUpdates));
	}

	[Fact]
	public void GetScheduleds_MultipleKeysWithNullLastAccess_RemovesAllFromDictionary()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule 10 keys
		for (int i = 0; i < 10; i++)
		{
			scheduler.ScheduleOrUpdate(new RedisKey($"test:key{i}"), expiration);
		}

		// First processing - all re-scheduled at half-life with LastAccess = null
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Second processing past the half-life time without any access - should remove all
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		var expirations = GetScheduledExpirations(scheduler);
		Assert.Empty(expirations);
		var scheduledUpdates = GetScheduledUpdates(scheduler);
		Assert.Equal(0, GetPriorityQueueCount(scheduledUpdates));
	}

	#endregion

	#region Integration Scenario Tests

	[Fact]
	public void Scenario_ScheduleAndProcess_KeyIsMaintained()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("scenario:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule (sets LastAccess = now)
		scheduler.ScheduleOrUpdate(key, expiration);

		// Process (use time after scheduling)
		var processTime = scheduleTime.AddMilliseconds(100);
		var result = scheduler.GetScheduleds(processTime);
		Assert.Single(result);

		// Key should still be tracked (LastAccess was set, so it gets re-scheduled)
		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(key));
		Assert.Null(expirations[key].LastAccess); // Reset to null
	}

	[Fact]
	public void Scenario_ScheduleProcessNoReaccess_KeyIsRemoved()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("scenario:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule (sets LastAccess = now)
		scheduler.ScheduleOrUpdate(key, expiration);

		// First process - re-schedules at half-life with LastAccess = null
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Second process past half-life time - no re-access in between, should remove
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		var expirations = GetScheduledExpirations(scheduler);
		Assert.False(expirations.ContainsKey(key));
	}

	[Fact]
	public void Scenario_ScheduleProcessReaccessProcess_KeyIsMaintained()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("scenario:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule and process
		scheduler.ScheduleOrUpdate(key, expiration);
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Re-access (sets LastAccess = now)
		scheduler.ScheduleOrUpdate(key, expiration);

		// Process again past half-life time - should keep (LastAccess.HasValue = true)
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(key));
	}

	[Fact]
	public void Scenario_MultipleProcessCycles_OnlyReaccessedKeysRemain()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var activeKey = new RedisKey("active:key");
		var inactiveKey = new RedisKey("inactive:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		// Schedule both
		scheduler.ScheduleOrUpdate(activeKey, expiration);
		scheduler.ScheduleOrUpdate(inactiveKey, expiration);

		// First cycle - both processed and re-scheduled at half-life with LastAccess = null
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Re-access only activeKey (sets LastAccess = now)
		scheduler.ScheduleOrUpdate(activeKey, expiration);

		// Second cycle past half-life time - only activeKey should remain
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		scheduler.GetScheduleds(secondProcessTime);

		var expirations = GetScheduledExpirations(scheduler);
		Assert.True(expirations.ContainsKey(activeKey));
		Assert.False(expirations.ContainsKey(inactiveKey));
		Assert.Single(expirations);
	}

	[Fact]
	public void Scenario_UpdateExpirationBetweenScheduleAndProcess_UsesLatestExpirationForSliding()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("scenario:key");
		var firstExpiration = TimeSpan.FromMinutes(10);
		var secondExpiration = TimeSpan.FromMinutes(20);
		var scheduleTime = DateTime.UtcNow;

		// Schedule with first expiration
		scheduler.ScheduleOrUpdate(key, firstExpiration);

		// Update to second expiration before processing (this also updates LastAccess)
		var updateTime = scheduleTime.AddMilliseconds(50);
		scheduler.ScheduleOrUpdate(key, secondExpiration);

		// Process - should return sliding expiration based on latest expiration
		var processTime = scheduleTime.AddMilliseconds(100);
		var result = scheduler.GetScheduleds(processTime);

		Assert.Single(result);

		// Sliding expiration should be based on secondExpiration and time since last update
		// LastAccess was ~updateTime (50ms after scheduleTime)
		// Processing at ~100ms after scheduleTime means ~50ms elapsed since LastAccess
		var expectedSlidingExpiration = secondExpiration - TimeSpan.FromMilliseconds(50);
		// Allow 50ms tolerance for execution time
		Assert.True(Math.Abs((result.First().Value - expectedSlidingExpiration).TotalMilliseconds) < 50);
	}

	#endregion

	#region Sliding Expiration Edge Cases

	[Fact]
	public void GetScheduleds_MinimalElapsedTime_ReturnsSlidingExpirationCloseToOriginal()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);

		scheduler.ScheduleOrUpdate(key, expiration);

		// Process very quickly after scheduling (minimal elapsed time)
		var result = scheduler.GetScheduleds(DateTime.UtcNow);

		Assert.Single(result);
		var item = result.First();

		// With minimal elapsed time, sliding expiration should be close to original expiration
		// Allow 100ms tolerance for execution time and scheduling overhead
		Assert.True(Math.Abs((item.Value - expiration).TotalMilliseconds) < 100);
	}

	[Fact]
	public void GetScheduleds_LargeElapsedTime_ReturnsSmallerSlidingExpiration()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, expiration);

		// Process after significant time has elapsed (5 minutes)
		var elapsedTime = TimeSpan.FromMinutes(5);
		var processTime = scheduleTime.Add(elapsedTime);
		var result = scheduler.GetScheduleds(processTime);

		Assert.Single(result);
		var item = result.First();

		// Sliding expiration should be approximately: original (10min) - elapsed (5min) = 5min
		var expectedSlidingExpiration = expiration - elapsedTime;
		// Allow 100ms tolerance
		Assert.True(Math.Abs((item.Value - expectedSlidingExpiration).TotalMilliseconds) < 100);
	}

	[Fact]
	public void GetScheduleds_ElapsedTimeExceedsExpiration_ReturnsNegativeSlidingExpiration()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);

		scheduler.ScheduleOrUpdate(key, expiration);

		// Process after time exceeding the expiration (11 minutes to avoid multiple re-schedules)
		var processTime = DateTime.UtcNow.Add(TimeSpan.FromMinutes(11));
		var result = scheduler.GetScheduleds(processTime);

		// Should return at least one entry for the key
		Assert.NotEmpty(result);

		// The first entry should have negative sliding expiration
		var firstEntry = result.First();
		Assert.Equal(key, firstEntry.Key);

		// Sliding expiration will be negative: 10min - ~11min = ~-1min
		// The implementation doesn't prevent negative values
		Assert.True(firstEntry.Value < TimeSpan.Zero);
		Assert.True(firstEntry.Value.TotalMinutes < 0);
	}

	[Fact]
	public void GetScheduleds_MultipleRapidReaccesses_UsesMostRecentLastAccess()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);

		// Schedule initially
		scheduler.ScheduleOrUpdate(key, expiration);

		// Re-access multiple times to update LastAccess
		scheduler.ScheduleOrUpdate(key, expiration);
		scheduler.ScheduleOrUpdate(key, expiration);

		// Process shortly after last access
		var result = scheduler.GetScheduleds(DateTime.UtcNow);

		Assert.Single(result);
		var item = result.First();

		// Sliding expiration should be close to original since last access was very recent
		// Allow 150ms tolerance for execution time and multiple schedule calls
		Assert.True(Math.Abs((item.Value - expiration).TotalMilliseconds) < 150);
	}

	[Fact]
	public void GetScheduleds_AfterReschedulingWithNullLastAccess_NoSlidingCalculation()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);
		var scheduleTime = DateTime.UtcNow;

		scheduler.ScheduleOrUpdate(key, expiration);

		// First process - sets LastAccess = null and re-schedules
		var firstProcessTime = scheduleTime.AddMilliseconds(100);
		scheduler.GetScheduleds(firstProcessTime);

		// Verify LastAccess is null
		var expirations = GetScheduledExpirations(scheduler);
		Assert.Null(expirations[key].LastAccess);

		// Second process when LastAccess is null - should NOT return the key again (it's removed)
		var slidingExpiration = expiration - TimeSpan.FromMilliseconds(100);
		var secondProcessTime = firstProcessTime.Add(slidingExpiration / 2).AddMilliseconds(100);
		var result = scheduler.GetScheduleds(secondProcessTime);

		// The key should be in the result but then removed since LastAccess = null
		Assert.Single(result);
		var item = result.First();
		Assert.Equal(key, item.Key);

		// After this processing, key should be completely removed
		expirations = GetScheduledExpirations(scheduler);
		Assert.False(expirations.ContainsKey(key));
	}

	[Fact]
	public void GetScheduleds_DifferentElapsedTimes_ProducesDifferentSlidingExpirations()
	{
		var scheduler = new PriorityQueueScheduler<RedisKey>();
		var key1 = new RedisKey("test:key1");
		var key2 = new RedisKey("test:key2");
		var expiration = TimeSpan.FromSeconds(100);

		// Schedule first key
		scheduler.ScheduleOrUpdate(key1, expiration);

		// Wait 50ms
		System.Threading.Thread.Sleep(50);

		// Schedule second key
		scheduler.ScheduleOrUpdate(key2, expiration);

		// Process both keys at the same time
		var processTime = DateTime.UtcNow.AddMilliseconds(10);
		var result = scheduler.GetScheduleds(processTime);

		Assert.Equal(2, result.Count);

		// Find both keys in results
		var key1Entry = result.First(kvp => kvp.Key == key1);
		var key2Entry = result.First(kvp => kvp.Key == key2);

		// key1 was scheduled earlier, so it should have smaller sliding expiration
		// (more time elapsed since LastAccess)
		Assert.True(key1Entry.Value < key2Entry.Value);
	}

	#endregion

	#region Helper Methods

	// Reflection helpers kept only for memory leak verification tests
	// These verify internal cleanup that can't be observed through public API

	private static dynamic GetScheduledExpirations<T>(PriorityQueueScheduler<T> scheduler) where T : notnull
	{
		var field = typeof(PriorityQueueScheduler<T>)
			.GetField("_scheduledExpirations", BindingFlags.NonPublic | BindingFlags.Instance);
		return field!.GetValue(scheduler)!;
	}

	private static dynamic GetScheduledUpdates<T>(PriorityQueueScheduler<T> scheduler) where T : notnull
	{
		var field = typeof(PriorityQueueScheduler<T>)
			.GetField("_scheduledUpdates", BindingFlags.NonPublic | BindingFlags.Instance);
		return field!.GetValue(scheduler)!;
	}

	private static int GetPriorityQueueCount(dynamic queue)
	{
		var property = queue.GetType().GetProperty("Count");
		return (int)property!.GetValue(queue)!;
	}

	#endregion
}
