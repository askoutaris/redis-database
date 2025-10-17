using NSubstitute;
using RedisDatabase.Exceptions;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.PeriodicTriggers;
using StackExchange.Redis;

namespace Tests.ExpirationUpdaters;

public class BackgroundExpirationUpdaterTests
{
	private readonly IPeriodicTrigger _periodicTrigger;
	private readonly IConnectionMultiplexer _connectionMultiplexer;
	private readonly IDatabase _database;
	private readonly IPriorityQueueScheduler<RedisKey> _scheduler;
	private AsyncEventHandler? _capturedHandler;

	public BackgroundExpirationUpdaterTests()
	{
		_periodicTrigger = Substitute.For<IPeriodicTrigger>();
		_connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		_database = Substitute.For<IDatabase>();
		_scheduler = Substitute.For<IPriorityQueueScheduler<RedisKey>>();

		// Setup connection multiplexer to return our mock database
		_connectionMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

		// Capture the handler registered to OnTrigger event
		_periodicTrigger.OnTrigger += Arg.Do<AsyncEventHandler>(handler => _capturedHandler = handler);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);

		Assert.NotNull(updater);
	}

	[Fact]
	public void Constructor_SubscribesToPeriodicTrigger()
	{
		_ = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);

		// Verify handler was captured (event was subscribed)
		Assert.NotNull(_capturedHandler);
	}

	#endregion

	#region UpdateLifetime Tests

	[Fact]
	public void UpdateLifetime_WithValidParameters_CallsScheduler()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(5);

		updater.UpdateLifetime(key, expiration);

		// Verify scheduler was called
		_scheduler.Received(1).ScheduleOrUpdate(key, expiration);
	}

	[Fact]
	public void UpdateLifetime_WithNullExpiration_DoesNothing()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);
		var key = new RedisKey("test:key");

		updater.UpdateLifetime(key, null);

		// Null expiration should be ignored - scheduler should NOT be called
		_scheduler.DidNotReceive().ScheduleOrUpdate(Arg.Any<RedisKey>(), Arg.Any<TimeSpan>());
	}

	[Fact]
	public void UpdateLifetime_WithSameKeyTwice_CallsSchedulerTwice()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);
		var key = new RedisKey("test:key");
		var firstExpiration = TimeSpan.FromMinutes(5);
		var secondExpiration = TimeSpan.FromMinutes(10);

		updater.UpdateLifetime(key, firstExpiration);
		updater.UpdateLifetime(key, secondExpiration);

		// Verify scheduler was called twice with different expirations
		_scheduler.Received(1).ScheduleOrUpdate(key, firstExpiration);
		_scheduler.Received(1).ScheduleOrUpdate(key, secondExpiration);
		_scheduler.Received(2).ScheduleOrUpdate(key, Arg.Any<TimeSpan>());
	}

	[Fact]
	public void UpdateLifetime_WithMultipleKeys_SchedulesAll()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);
		var key1 = new RedisKey("test:key1");
		var key2 = new RedisKey("test:key2");
		var expiration1 = TimeSpan.FromMinutes(5);
		var expiration2 = TimeSpan.FromMinutes(10);

		updater.UpdateLifetime(key1, expiration1);
		updater.UpdateLifetime(key2, expiration2);

		// Verify both keys were scheduled
		_scheduler.Received(1).ScheduleOrUpdate(key1, expiration1);
		_scheduler.Received(1).ScheduleOrUpdate(key2, expiration2);
	}

	#endregion

	#region UpdateLifetimes Processing Tests

	[Fact]
	public async Task UpdateLifetimes_WithNoExpirations_DoesNothing()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);

		// Mock scheduler to return empty collection
		_scheduler.GetScheduleds(Arg.Any<DateTime>()).Returns(Array.Empty<KeyValuePair<RedisKey, TimeSpan>>());

		// Trigger the UpdateLifetimes method via event handler
		await _capturedHandler!();

		// Since there are no expirations, should not call GetDatabase
		_connectionMultiplexer.DidNotReceive().GetDatabase();
	}

	[Fact]
	public async Task UpdateLifetimes_WithSingleExpiration_ProcessesCorrectly()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);
		var key = new RedisKey("test:key");
		var expiration = TimeSpan.FromMinutes(10);

		// Mock scheduler to return one due item
		var dueItems = new[] { new KeyValuePair<RedisKey, TimeSpan>(key, expiration) };
		_scheduler.GetScheduleds(Arg.Any<DateTime>()).Returns(dueItems);

		// Mock the batch operations
		var batch = Substitute.For<IBatch>();
		var keyExpireTask = Task.FromResult(true);
		batch.KeyExpireAsync(key, expiration).Returns(keyExpireTask);
		_database.CreateBatch().Returns(batch);

		// Trigger the UpdateLifetimes method via event handler
		await _capturedHandler!();

		// Verify Redis operations were called
		_connectionMultiplexer.Received(1).GetDatabase();
		await batch.Received(1).KeyExpireAsync(key, expiration);
		batch.Received(1).Execute();
	}

	[Fact]
	public async Task UpdateLifetimes_WithMultipleExpirations_ProcessesInChunks()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);

		// Create 1500 due items to test chunking (should create 2 chunks of 1000 and 500)
		var dueItems = Enumerable.Range(0, 1500)
			.Select(i => new KeyValuePair<RedisKey, TimeSpan>(
				new RedisKey($"test:key{i}"),
				TimeSpan.FromMinutes(i + 1)))
			.ToArray();

		_scheduler.GetScheduleds(Arg.Any<DateTime>()).Returns(dueItems);

		var batch = Substitute.For<IBatch>();
		batch.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>()).Returns(Task.FromResult(true));
		_database.CreateBatch().Returns(batch);

		await _capturedHandler!();

		// Should call GetDatabase twice (once for each chunk)
		_connectionMultiplexer.Received(2).GetDatabase();

		// Should execute batch twice
		batch.Received(2).Execute();
	}

	[Fact]
	public async Task UpdateLifetimes_WithExactly1000Expirations_ProcessesInSingleChunk()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);

		// Create exactly 1000 due items
		var dueItems = Enumerable.Range(0, 1000)
			.Select(i => new KeyValuePair<RedisKey, TimeSpan>(
				new RedisKey($"test:key{i}"),
				TimeSpan.FromMinutes(i + 1)))
			.ToArray();

		_scheduler.GetScheduleds(Arg.Any<DateTime>()).Returns(dueItems);

		var batch = Substitute.For<IBatch>();
		batch.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>()).Returns(Task.FromResult(true));
		_database.CreateBatch().Returns(batch);

		await _capturedHandler!();

		// Should call GetDatabase once
		_connectionMultiplexer.Received(1).GetDatabase();

		// Should execute batch once
		batch.Received(1).Execute();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task UpdateLifetimes_WhenConnectionMultiplexerGetDatabaseThrows_PropagatesException()
	{
		// Create a connection multiplexer that throws when getting database
		var throwingMultiplexer = Substitute.For<IConnectionMultiplexer>();
		throwingMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>())
			.Returns(x => throw new InvalidOperationException("Database unavailable"));

		// Need to recapture handler with new trigger setup
		AsyncEventHandler? handler = null;
		_periodicTrigger.OnTrigger += Arg.Do<AsyncEventHandler>(h => handler = h);

		var updater = new BackgroundExpirationUpdater(throwingMultiplexer, _scheduler, _periodicTrigger);

		// Mock scheduler to return one due item
		var dueItems = new[] { new KeyValuePair<RedisKey, TimeSpan>(
			new RedisKey("test:key"),
			TimeSpan.FromMinutes(5)) };
		_scheduler.GetScheduleds(Arg.Any<DateTime>()).Returns(dueItems);

		await Assert.ThrowsAsync<InvalidOperationException>(() => handler!());
	}

	[Fact]
	public async Task UpdateLifetimes_WhenBatchExecuteThrows_PropagatesException()
	{
		var updater = new BackgroundExpirationUpdater(_connectionMultiplexer, _scheduler, _periodicTrigger);

		// Mock scheduler to return one due item
		var dueItems = new[] { new KeyValuePair<RedisKey, TimeSpan>(
			new RedisKey("test:key"),
			TimeSpan.FromMinutes(5)) };
		_scheduler.GetScheduleds(Arg.Any<DateTime>()).Returns(dueItems);

		var batch = Substitute.For<IBatch>();
		batch.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>()).Returns(Task.FromResult(true));
		var innerException = new RedisException("Redis error");
		batch.When(x => x.Execute()).Do(x => throw innerException);
		_database.CreateBatch().Returns(batch);

		var exception = await Assert.ThrowsAsync<RedisStackExchangeException>(() => _capturedHandler!());

		Assert.Equal("Redis error", exception.Message);
		Assert.Same(innerException, exception.InnerException);
	}

	#endregion
}
