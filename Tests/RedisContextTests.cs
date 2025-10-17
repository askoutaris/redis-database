using NSubstitute;
using RedisDatabase;
using RedisDatabase.Exceptions;
using StackExchange.Redis;

namespace Tests;

public class RedisContextTests
{
	private readonly IDatabase _database;
	private readonly RedisContext _redisContext;

	public RedisContextTests()
	{
		_database = Substitute.For<IDatabase>();
		_redisContext = new RedisContext(_database);
	}

	[Fact]
	public void Constructor_InitializesPropertiesCorrectly()
	{
		Assert.Same(_database, _redisContext.Database);
	}

	[Fact]
	public void Constructor_WithNullDatabase_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new RedisContext(null!));

		Assert.Equal("db", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var database = Substitute.For<IDatabase>();

		var context = new RedisContext(database);

		Assert.Same(database, context.Database);
		Assert.NotNull(context);
	}

	[Fact]
	public void Database_ReturnsCorrectInstance()
	{
		Assert.Same(_database, _redisContext.Database);
	}

	[Fact]
	public void Batch_CreatesBatchOnFirstAccess()
	{
		var batch = Substitute.For<IBatch>();
		_database.CreateBatch().Returns(batch);

		var actualBatch = _redisContext.Batch;

		Assert.Same(batch, actualBatch);
		_database.Received(1).CreateBatch();
	}

	[Fact]
	public void Batch_ReturnsSameBatchOnSubsequentCalls()
	{
		var batch = Substitute.For<IBatch>();
		_database.CreateBatch().Returns(batch);

		var batch1 = _redisContext.Batch;
		var batch2 = _redisContext.Batch;

		Assert.Same(batch1, batch2);
		_database.Received(1).CreateBatch();
	}

	[Fact]
	public async Task AddBatch_ExecutesActionWithBatch()
	{
		var batch = Substitute.For<IBatch>();
		_database.CreateBatch().Returns(batch);
		var expectedResult = "test result";
		var actionExecuted = false;

		var result = await _redisContext.AddBatch<string>(b =>
		{
			actionExecuted = true;
			Assert.Same(batch, b);
			return Task.FromResult(expectedResult);
		});

		Assert.True(actionExecuted);
		Assert.Equal(expectedResult, result);
	}

	[Fact]
	public void AddCommand_AddsActionToList()
	{
		var actionExecuted = false;
		Task action(IDatabaseAsync db)
		{
			actionExecuted = true;
			return Task.CompletedTask;
		}

		_redisContext.AddCommand(action);

		Assert.False(actionExecuted);
	}

	[Fact]
	public void AddCondition_AddsConditionToList()
	{
		var condition = Condition.StringEqual("key", "value");

		_redisContext.AddCondition(condition);

		// No direct way to verify, but we'll test it in Commit tests
	}


	[Fact]
	public async Task Commit_WithSingleActionAndNoConditions_ExecutesIndividually()
	{
		var actionExecuted = false;
		_redisContext.AddCommand(db =>
		{
			actionExecuted = true;
			Assert.Same(_database, db);
			return Task.CompletedTask;
		});

		await _redisContext.Commit();

		Assert.True(actionExecuted);
	}

	[Fact]
	public async Task Commit_WithMultipleActions_ExecutesTransactionally()
	{
		var transaction = Substitute.For<ITransaction>();
		_database.CreateTransaction().Returns(transaction);
		transaction.ExecuteAsync().Returns(true);

		var action1Executed = false;
		var action2Executed = false;

		_redisContext.AddCommand(db =>
		{
			action1Executed = true;
			return Task.CompletedTask;
		});

		_redisContext.AddCommand(db =>
		{
			action2Executed = true;
			return Task.CompletedTask;
		});

		await _redisContext.Commit();

		Assert.True(action1Executed);
		Assert.True(action2Executed);
		await transaction.Received(1).ExecuteAsync();
	}

	[Fact]
	public async Task Commit_WithConditions_ExecutesTransactionally()
	{
		var transaction = Substitute.For<ITransaction>();
		_database.CreateTransaction().Returns(transaction);
		transaction.ExecuteAsync().Returns(true);

		var condition = Condition.StringEqual("key", "value");
		_redisContext.AddCondition(condition);
		_redisContext.AddCommand(db => Task.CompletedTask);

		await _redisContext.Commit();

		transaction.Received(1).AddCondition(condition);
		await transaction.Received(1).ExecuteAsync();
	}

	[Fact]
	public async Task Commit_WhenTransactionFails_ThrowsVersionConflictException()
	{
		var transaction = Substitute.For<ITransaction>();
		_database.CreateTransaction().Returns(transaction);
		transaction.ExecuteAsync().Returns(false);

		_redisContext.AddCommand(db => Task.CompletedTask);
		_redisContext.AddCommand(db => Task.CompletedTask);

		var exception = await Assert.ThrowsAsync<RedisConflictException>(() => _redisContext.Commit());
		Assert.Equal("Redis transaction failed - VersionConflict", exception.Message);
	}

	[Fact]
	public async Task Commit_AfterFirstUse_ThrowsInvalidOperationException()
	{
		_redisContext.AddCommand(db => Task.CompletedTask);
		await _redisContext.Commit();

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _redisContext.Commit());
		Assert.Equal("RedisContext can be used for one operation only - consider using a new one", exception.Message);
	}

	[Fact]
	public async Task ExecuteBatch_ExecutesBatchAndWaitsForTasks()
	{
		var batch = Substitute.For<IBatch>();
		_database.CreateBatch().Returns(batch);

		var task1Completed = false;
		var task2Completed = false;

		await _redisContext.AddBatch<string>(async b =>
		{
			await Task.Delay(10);
			task1Completed = true;
			return "result1";
		});

		await _redisContext.AddBatch<string>(async b =>
		{
			await Task.Delay(10);
			task2Completed = true;
			return "result2";
		});

		await _redisContext.ExecuteBatch();

		Assert.True(task1Completed);
		Assert.True(task2Completed);
		batch.Received(1).Execute();
	}

	[Fact]
	public async Task ExecuteBatch_AfterFirstUse_ThrowsInvalidOperationException()
	{
		await _redisContext.ExecuteBatch();

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _redisContext.ExecuteBatch());
		Assert.Equal("RedisContext can be used for one operation only - consider using a new one", exception.Message);
	}

	[Fact]
	public async Task ExecuteBatch_WhenBatchTaskThrows_WrapsInRedisStackExchangeException()
	{
		var batch = Substitute.For<IBatch>();
		_database.CreateBatch().Returns(batch);

		var innerException = new InvalidOperationException("Redis operation failed");
		_ = _redisContext.AddBatch<string>(b =>
		{
			return Task.FromException<string>(innerException);
		});

		var exception = await Assert.ThrowsAsync<RedisStackExchangeException>(() => _redisContext.ExecuteBatch());

		Assert.Equal("Redis operation failed", exception.Message);
		Assert.Same(innerException, exception.InnerException);
	}

	[Fact]
	public async Task CommitIndividually_ExecutesAllActionsSequentially()
	{
		var executionOrder = new List<int>();

		_redisContext.AddCommand(async db =>
		{
			await Task.Delay(10);
			executionOrder.Add(1);
		});

		await _redisContext.Commit();

		Assert.Single(executionOrder);
		Assert.Equal(1, executionOrder[0]);
	}

	[Fact]
	public async Task CommitIndividually_WhenActionThrows_WrapsInRedisStackExchangeException()
	{
		var innerException = new InvalidOperationException("Redis operation failed");
		_redisContext.AddCommand(db =>
		{
			throw innerException;
		});

		var exception = await Assert.ThrowsAsync<RedisStackExchangeException>(() => _redisContext.Commit());

		Assert.Equal("Redis operation failed", exception.Message);
		Assert.Same(innerException, exception.InnerException);
	}

	[Fact]
	public async Task CommitTransactionally_AddsAllConditionsToTransaction()
	{
		var transaction = Substitute.For<ITransaction>();
		_database.CreateTransaction().Returns(transaction);
		transaction.ExecuteAsync().Returns(true);

		var condition1 = Condition.StringEqual("key1", "value1");
		var condition2 = Condition.StringEqual("key2", "value2");

		_redisContext.AddCondition(condition1);
		_redisContext.AddCondition(condition2);
		_redisContext.AddCommand(db => Task.CompletedTask);

		await _redisContext.Commit();

		transaction.Received(1).AddCondition(condition1);
		transaction.Received(1).AddCondition(condition2);
	}

	[Fact]
	public async Task CommitTransactionally_ExecutesAllActionsInTransaction()
	{
		var transaction = Substitute.For<ITransaction>();
		_database.CreateTransaction().Returns(transaction);
		transaction.ExecuteAsync().Returns(true);

		var action1Executed = false;
		var action2Executed = false;

		_redisContext.AddCommand(db =>
		{
			action1Executed = true;
			Assert.Same(transaction, db);
			return Task.CompletedTask;
		});

		_redisContext.AddCommand(db =>
		{
			action2Executed = true;
			Assert.Same(transaction, db);
			return Task.CompletedTask;
		});

		await _redisContext.Commit();

		Assert.True(action1Executed);
		Assert.True(action2Executed);
	}

	[Fact]
	public async Task CommitTransactional_WhenExecuteAsyncThrows_WrapsInRedisStackExchangeException()
	{
		var transaction = Substitute.For<ITransaction>();
		_database.CreateTransaction().Returns(transaction);

		var innerException = new InvalidOperationException("Redis transaction execute failed");
		transaction.ExecuteAsync().Returns<bool>(_ => throw innerException);

		_redisContext.AddCommand(db => Task.CompletedTask);
		_redisContext.AddCommand(db => Task.CompletedTask);

		var exception = await Assert.ThrowsAsync<RedisStackExchangeException>(() => _redisContext.Commit());

		Assert.Equal("Redis transaction execute failed", exception.Message);
		Assert.Same(innerException, exception.InnerException);
	}

	[Fact]
	public void EnsureNotUsed_AllowsFirstCall()
	{
		// This should not throw
		_redisContext.AddCommand(db => Task.CompletedTask);
	}

	[Fact]
	public async Task EnsureNotUsed_ThrowsOnSecondCall()
	{
		await _redisContext.Commit();

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _redisContext.Commit());
		Assert.Equal("RedisContext can be used for one operation only - consider using a new one", exception.Message);
	}

	[Fact]
	public async Task Parallel_Commit_Calls_ThrowInvalidOperationException()
	{
		_redisContext.AddCommand(db => Task.CompletedTask);

		var task1 = _redisContext.Commit();
		var task2 = _redisContext.Commit();

		// One should succeed, one should fail
		var results = await Task.WhenAll(
			Task.Run(async () => { try { await task1; return true; } catch (InvalidOperationException) { return false; } }),
			Task.Run(async () => { try { await task2; return true; } catch (InvalidOperationException) { return false; } })
		);

		// Exactly one should succeed and one should fail
		Assert.True(results.Count(r => r) == 1 && results.Count(r => !r) == 1);
	}

	[Fact]
	public async Task Commit_WithNoActions_CompletesSuccessfully()
	{
		await _redisContext.Commit();
		// Should complete without errors
	}

	[Fact]
	public async Task ExecuteBatch_WithNoTasks_CompletesSuccessfully()
	{
		var batch = Substitute.For<IBatch>();
		_database.CreateBatch().Returns(batch);

		await _redisContext.ExecuteBatch();

		batch.Received(1).Execute();
	}
}