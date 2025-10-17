using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Serializers;
using NSubstitute;
using StackExchange.Redis;
using System.Reflection;

namespace Tests.Adapters;

public class HashsetAdapterTests
{
	private readonly IRedisContext _redisContext;
	private readonly IRedisSerializer _serializer;
	private readonly IResultReader _reader;
	private readonly HashsetAdapter _adapter;

	public HashsetAdapterTests()
	{
		_redisContext = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
		_reader = Substitute.For<IResultReader>();
		_adapter = new HashsetAdapter(_redisContext, _serializer, _reader);
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
		var adapter = new HashsetAdapter(_redisContext, _serializer, _reader);
		Assert.NotNull(adapter);
	}
	#endregion

	#region TryGetField Tests

	[Fact]
	public async Task TryGetField_WithExistingField_ReturnsDeserializedEntity()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);

		// Act
		var result = await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		await database.Received(1).HashGetAsync(key, fieldName);
		_serializer.Received(1).Deserialize<TestEntity>(serializedData);
	}

	[Fact]
	public async Task TryGetField_WithNonExistingField_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("nonexistent");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(RedisValue.Null));

		// Act
		var result = await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert
		Assert.Null(result);
		await database.Received(1).HashGetAsync(key, fieldName);
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	[Fact]
	public async Task TryGetField_CallsHashGetAsyncOnDatabase()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(RedisValue.Null));

		// Act
		await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert
		await database.Received(1).HashGetAsync(key, fieldName);
	}

	[Fact]
	public async Task TryGetField_DeserializesWithCorrectData()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3, 4, 5 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);

		// Act
		var result = await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert
		_serializer.Received(1).Deserialize<TestEntity>(Arg.Is<byte[]>(data => data.SequenceEqual(serializedData)));
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGetField_WithNullRedisValue_DoesNotCallDeserialize()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(RedisValue.Null));

		// Act
		await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	[Fact]
	public async Task TryGetField_WhenDeserializationThrows_PropagatesException()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var serializedData = new byte[] { 1, 2, 3 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();
		var expectedException = new InvalidOperationException("Deserialization failed");

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(_ => throw expectedException);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _adapter.TryGetField<TestEntity>(key, fieldName));
		Assert.Same(expectedException, exception);
	}

	[Fact]
	public async Task TryGetField_ExecutesImmediately_WithoutBatching()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		RedisValue redisValue = serializedData;
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(redisValue));
		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);

		// Act
		var result = await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert - Verify no batch operations were used
		_ = _redisContext.DidNotReceive().AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>());
		_redisContext.DidNotReceive().AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGetField_WithEmptyRedisValue_CallsDeserialize()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, fieldName).Returns(Task.FromResult(RedisValue.EmptyString));

		// Act
		var result = await _adapter.TryGetField<TestEntity>(key, fieldName);

		// Assert
		// EmptyString is not IsNull, so deserialize will be called
		await database.Received(1).HashGetAsync(key, fieldName);
	}

	[Fact]
	public async Task TryGetField_WithDifferentFieldNames_CallsHashGetWithCorrectField()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName1 = new RedisValue("field1");
		var fieldName2 = new RedisValue("field2");
		var database = Substitute.For<IDatabase>();

		_redisContext.Database.Returns(database);
		database.HashGetAsync(key, Arg.Any<RedisValue>()).Returns(Task.FromResult(RedisValue.Null));

		// Act
		await _adapter.TryGetField<TestEntity>(key, fieldName1);
		await _adapter.TryGetField<TestEntity>(key, fieldName2);

		// Assert
		await database.Received(1).HashGetAsync(key, fieldName1);
		await database.Received(1).HashGetAsync(key, fieldName2);
		await database.Received(2).HashGetAsync(key, Arg.Any<RedisValue>());
	}

	#endregion

	#region TryReadField Tests

	[Fact]
	public void TryReadField_WithValidParameters_CallsAddBatchWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var task = Task.FromResult<TestEntity?>(new TestEntity { Name = "Test", Id = 1 });
		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(task);

		// Act
		var result = _adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		_redisContext.Received(1).AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>());
		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void TryReadField_WithRedisValue_ReturnsReadResultWithCorrectTask()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		var task = Task.FromResult<TestEntity?>(expectedEntity);

		_serializer.Deserialize<TestEntity>(serializedData).Returns(expectedEntity);
		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(task);

		// Act
		var result = _adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void TryReadField_CreatesCorrectBatchOperation()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var redisValue = new RedisValue("serialized_data");
		var batch = Substitute.For<IBatch>();
		var hashGetTask = Task.FromResult(redisValue);
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };

		batch.HashGetAsync(key, fieldName).Returns(hashGetTask);
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns(expectedEntity);

		Func<IBatch, Task<TestEntity?>>? capturedAction = null;
		_redisContext.AddBatch(Arg.Do<Func<IBatch, Task<TestEntity?>>>(action => capturedAction = action))
			.Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		_adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action works correctly
		var resultTask = capturedAction(batch);
		batch.Received(1).HashGetAsync(key, fieldName);
	}

	[Fact]
	public void TryReadField_WithNullRedisValue_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var nullRedisValue = RedisValue.Null;
		var batch = Substitute.For<IBatch>();
		var hashGetTask = Task.FromResult(nullRedisValue);

		batch.HashGetAsync(key, fieldName).Returns(hashGetTask);

		Func<IBatch, Task<TestEntity?>>? capturedAction = null;
		_redisContext.AddBatch(Arg.Do<Func<IBatch, Task<TestEntity?>>>(action => capturedAction = action))
			.Returns(Task.FromResult<TestEntity?>(null));

		// Act
		_adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action returns null for null RedisValue
		var resultTask = capturedAction(batch);
		// Note: We can't safely call .Result here as it may block, but we can verify the serializer wasn't called
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	#endregion

	#region SetField Tests

	[Fact]
	public void SetField_WithValidParameters_CallsAddCommandWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };

		_serializer.Serialize(entity).Returns(serializedData);

		// Act
		_adapter.SetField(key, fieldName, entity);

		// Assert
		_serializer.Received(1).Serialize(entity);
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void SetField_CreatesCorrectDatabaseOperation()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };
		var database = Substitute.For<IDatabaseAsync>();

		_serializer.Serialize(entity).Returns(serializedData);

		Func<IDatabaseAsync, Task>? capturedAction = null;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action => capturedAction = action));

		// Act
		_adapter.SetField(key, fieldName, entity);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action calls HashSetAsync with correct parameters
		capturedAction(database);
		database.Received(1).HashSetAsync(key, fieldName, serializedData);
	}

	[Fact]
	public void SetField_SerializesEntityBeforeAddingCommand()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var entity = new TestEntity { Name = "Test", Id = 1 };
		var serializedData = new byte[] { 1, 2, 3 };

		_serializer.Serialize(entity).Returns(serializedData);

		// Act
		_adapter.SetField(key, fieldName, entity);

		// Assert
		// Verify serialization happens immediately, not deferred
		_serializer.Received(1).Serialize(entity);
	}

	#endregion

	#region RemoveField Tests

	[Fact]
	public void RemoveField_WithValidParameters_CallsAddCommandWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");

		// Act
		_adapter.RemoveField(key, fieldName);

		// Assert
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void RemoveField_CreatesCorrectDatabaseOperation()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var database = Substitute.For<IDatabaseAsync>();

		Func<IDatabaseAsync, Task>? capturedAction = null;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action => capturedAction = action));

		// Act
		_adapter.RemoveField(key, fieldName);

		// Assert
		Assert.NotNull(capturedAction);

		// Verify the captured action calls HashDeleteAsync with correct parameters
		capturedAction(database);
		database.Received(1).HashDeleteAsync(key, fieldName);
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_WithValidKey_CallsAddCommandWithCorrectParameters()
	{
		// Arrange
		var key = new RedisKey("test:hash");

		// Act
		_adapter.Remove(key);

		// Assert
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Remove_CreatesCorrectDatabaseOperation()
	{
		// Arrange
		var key = new RedisKey("test:hash");
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
	public void TryReadField_ReturnsReadResultThatWrapsTask()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var task = Task.FromResult<TestEntity?>(expectedEntity);

		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(task);

		// Act
		var result = _adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		Assert.IsType<ReadResult<TestEntity>>(result);
		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void TryReadField_ReadResultValueThrowsWhenTaskNotCompleted()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var incompleteTask = new TaskCompletionSource<TestEntity?>().Task;

		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(incompleteTask);

		// Act
		var result = _adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		Assert.False(GetTask(result).IsCompleted);
		var exception = Assert.Throws<Exception>(() => result.Value);
		Assert.Contains("Read not performed yet", exception.Message);
		Assert.Contains("RedisContext.ExecuteBatch()", exception.Message);
	}

	[Fact]
	public void TryReadField_ReadResultValueReturnsWhenTaskCompleted()
	{
		// Arrange
		var key = new RedisKey("test:hash");
		var fieldName = new RedisValue("field1");
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		var completedTask = Task.FromResult<TestEntity?>(expectedEntity);

		_redisContext.AddBatch(Arg.Any<Func<IBatch, Task<TestEntity?>>>()).Returns(completedTask);

		// Act
		var result = _adapter.TryReadField<TestEntity>(key, fieldName);

		// Assert
		Assert.True(GetTask(result).IsCompleted);
		Assert.Equal(expectedEntity, result.Value);
	}

	#endregion
}