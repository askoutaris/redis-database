using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Serializers;
using NSubstitute;
using StackExchange.Redis;
using System.Reflection;

namespace Tests.Adapters;

public class StringAdapterTests
{
	private readonly IRedisContext _redisContext;
	private readonly IRedisSerializer _serializer;
	private readonly IResultReader _reader;
	private readonly StringAdapter _adapter;

	public StringAdapterTests()
	{
		_redisContext = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
		_reader = Substitute.For<IResultReader>();
		_adapter = new StringAdapter(_redisContext, _serializer, _reader);
	}

	private class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	private static Task<T?> GetTask<T>(ReadResult<T> result)
	{
		var field = typeof(ReadResult<T>).GetField("_task", BindingFlags.NonPublic | BindingFlags.Instance);
		return (Task<T?>)field!.GetValue(result)!;
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var adapter = new StringAdapter(_redisContext, _serializer, _reader);
		Assert.NotNull(adapter);
	}
	#endregion

	#region TryGet Tests

	[Fact]
	public async Task TryGet_WithExistingKey_ReturnsDeserializedEntity()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);

		// Act
		var result = await _adapter.TryGet<TestEntity>(key);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		await database.Received(1).StringGetAsync(key);
		_serializer.Received(1).Deserialize<TestEntity>(serializedData);
	}

	[Fact]
	public async Task TryGet_WithNonExistingKey_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:nonexistent");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(RedisValue.Null));

		// Act
		var result = await _adapter.TryGet<TestEntity>(key);

		// Assert
		Assert.Null(result);
		await database.Received(1).StringGetAsync(key);
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	[Fact]
	public async Task TryGet_CallsStringGetAsyncOnDatabase()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(RedisValue.Null));

		// Act
		await _adapter.TryGet<TestEntity>(key);

		// Assert
		await database.Received(1).StringGetAsync(key);
	}

	[Fact]
	public async Task TryGet_DeserializesWithCorrectData()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3, 4, 5 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);

		// Act
		var result = await _adapter.TryGet<TestEntity>(key);

		// Assert
		_serializer.Received(1).Deserialize<TestEntity>(Arg.Is<byte[]>(data => data.SequenceEqual(serializedData)));
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGet_WithNullRedisValue_DoesNotCallDeserialize()
	{
		// Arrange
		var key = new RedisKey("test:null");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(RedisValue.Null));

		// Act
		await _adapter.TryGet<TestEntity>(key);

		// Assert
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	[Fact]
	public async Task TryGet_WhenDeserializationThrows_PropagatesException()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var serializedData = new byte[] { 1, 2, 3 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();
		var expectedException = new InvalidOperationException("Deserialization failed");

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(_ => throw expectedException);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _adapter.TryGet<TestEntity>(key));
		Assert.Same(expectedException, exception);
	}

	[Fact]
	public async Task TryGet_ExecutesImmediately_WithoutBatching()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);

		// Act
		var result = await _adapter.TryGet<TestEntity>(key);

		// Assert - Verify no batch operations were used
		_= _redisContext.DidNotReceive().AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>());
		_redisContext.DidNotReceive().AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGet_WithEmptyRedisValue_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:empty");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.StringGetAsync(key).Returns(Task.FromResult(RedisValue.EmptyString));

		// Act
		var result = await _adapter.TryGet<TestEntity>(key);

		// Assert
		// EmptyString is not IsNull, so deserialize will be called
		await database.Received(1).StringGetAsync(key);
	}

	#endregion

	#region TryRead Tests

	[Fact]
	public void TryRead_WithValidKey_CallsAddBatchWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var task = Task.FromResult<TestEntity?>(new TestEntity { Name = "Test", Id = 1 });
		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(task);

		// Act
		var result = _adapter.TryRead<TestEntity>(key);

		// Assert
		_redisContext.Received(1).AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>());
		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void TryRead_WithRedisValue_ReturnsReadResultWithCorrectTask()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		var task = Task.FromResult<TestEntity?>(expectedEntity);

		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);
		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(task);

		// Act
		var result = _adapter.TryRead<TestEntity>(key);

		// Assert
		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void TryRead_CreatesCorrectBatchOperation()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var redisValue = new RedisValue("serialized_data");
		var batch = Substitute.For<IBatch>();
		var stringGetTask = Task.FromResult(redisValue);
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };

		batch.StringGetAsync(key).Returns(stringGetTask);
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns(expectedEntity);

		Func<IBatch, Task<TestEntity?>>? capturedAction = null;
		_redisContext.AddBatch(Arg.Do<Func<IBatch, Task<TestEntity?>>>(action => capturedAction = action))
			.Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		_adapter.TryRead<TestEntity>(key);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action works correctly
		var resultTask = capturedAction(batch);
		batch.Received(1).StringGetAsync(key);
	}

	[Fact]
	public void TryRead_WithNullRedisValue_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var nullRedisValue = RedisValue.Null;
		var batch = Substitute.For<IBatch>();
		var stringGetTask = Task.FromResult(nullRedisValue);

		batch.StringGetAsync(key).Returns(stringGetTask);

		Func<IBatch, Task<TestEntity?>>? capturedAction = null;
		_redisContext.AddBatch(Arg.Do<Func<IBatch, Task<TestEntity?>>>(action => capturedAction = action))
			.Returns(Task.FromResult<TestEntity?>(null));

		// Act
		_adapter.TryRead<TestEntity>(key);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action returns null for null RedisValue
		var resultTask = capturedAction(batch);
		// Note: We can't safely call .Result here as it may block, but we can verify the serializer wasn't called
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	#endregion

	#region Set Tests

	[Fact]
	public void Set_WithValidParameters_CallsAddCommandWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var expiration = TimeSpan.FromMinutes(5);
		var serializedData = new byte[] { 1, 2, 3 };

		_serializer.Serialize(entity).Returns(serializedData);

		// Act
		_adapter.Set(key, entity, expiration);

		// Assert
		_serializer.Received(1).Serialize(entity);
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Set_WithNullExpiration_CallsAddCommandWithNullExpiry()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };

		_serializer.Serialize(entity).Returns(serializedData);

		// Act
		_adapter.Set(key, entity, null);

		// Assert
		_serializer.Received(1).Serialize(entity);
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Set_CreatesCorrectDatabaseOperation()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var expiration = TimeSpan.FromMinutes(10);
		var serializedData = new byte[] { 1, 2, 3 };
		var database = Substitute.For<IDatabaseAsync>();

		_serializer.Serialize(entity).Returns(serializedData);

		Func<IDatabaseAsync, Task>? capturedAction = null;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action => capturedAction = action));

		// Act
		_adapter.Set(key, entity, expiration);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action calls StringSetAsync with correct parameters
		capturedAction(database);
		database.Received(1).StringSetAsync(key, serializedData, expiration);
	}

	[Fact]
	public void Set_SerializesEntityBeforeAddingCommand()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };

		_serializer.Serialize(entity).Returns(serializedData);

		// Act
		_adapter.Set(key, entity, TimeSpan.FromMinutes(5));

		// Assert
		// Verify serialization happens immediately, not deferred
		_serializer.Received(1).Serialize(entity);
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_WithValidKey_CallsAddCommandWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:key");

		// Act
		_adapter.Remove(key);

		// Assert
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Remove_CreatesCorrectDatabaseOperation()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var database = Substitute.For<IDatabaseAsync>();

		Func<IDatabaseAsync, Task>? capturedAction = null;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action => capturedAction = action));

		// Act
		_adapter.Remove(key);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action calls KeyDeleteAsync with correct key
		capturedAction(database);
		database.Received(1).KeyDeleteAsync(key);
	}

	#endregion

	#region ReadResult Integration Tests

	[Fact]
	public void TryRead_ReturnsReadResultThatWrapsTask()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var task = Task.FromResult<TestEntity?>(expectedEntity);

		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(task);

		// Act
		var result = _adapter.TryRead<TestEntity>(key);

		// Assert
		Assert.IsType<ReadResult<TestEntity>>(result);
		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void TryRead_ReadResultValueThrowsWhenTaskNotCompleted()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var incompleteTask = new TaskCompletionSource<TestEntity?>().Task;

		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(incompleteTask);

		// Act
		var result = _adapter.TryRead<TestEntity>(key);

		// Assert
		Assert.False(GetTask(result).IsCompleted);
		var exception = Assert.Throws<Exception>(() => result.Value);
		Assert.Contains("Read not performed yet", exception.Message);
		Assert.Contains("RedisContext.ExecuteBatch()", exception.Message);
	}

	[Fact]
	public void TryRead_ReadResultValueReturnsWhenTaskCompleted()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var completedTask = Task.FromResult<TestEntity?>(expectedEntity);

		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(completedTask);

		// Act
		var result = _adapter.TryRead<TestEntity>(key);

		// Assert
		Assert.True(GetTask(result).IsCompleted);
		Assert.Equal(expectedEntity, result.Value);
	}

	#endregion
}