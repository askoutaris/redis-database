using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Serializers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;

namespace Tests.Adapters;

public class ResultReaderTests
{
	private readonly ILogger _logger;
	private readonly IRedisContext _context;
	private readonly IRedisSerializer _serializer;
	private readonly ResultReader _reader;

	public ResultReaderTests()
	{
		_logger = Substitute.For<ILogger>();
		_context = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
		_reader = new ResultReader(_logger);
	}

	private class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidLogger_InitializesCorrectly()
	{
		var reader = new ResultReader(_logger);
		Assert.NotNull(reader);
	}

	#endregion

	#region Read Tests - Success Cases

	[Fact]
	public void Read_WithValidRedisValue_DeserializesAndReturnsEntity()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns(expectedEntity);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		_serializer.Received(1).Deserialize<TestEntity>(Arg.Is<byte[]>(b => b.SequenceEqual(bytes)));
	}

	[Fact]
	public void Read_WithValidRedisValue_DoesNotLogError()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var expectedEntity = new TestEntity { Name = "Test", Id = 1 };
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns(expectedEntity);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		_logger.DidNotReceive().Log(
			LogLevel.Error,
			Arg.Any<EventId>(),
			Arg.Any<object>(),
			Arg.Any<Exception>(),
			Arg.Any<Func<object, Exception?, string>>());
	}

	#endregion

	#region Read Tests - Null Value Cases

	[Fact]
	public void Read_WithNullRedisValue_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var task = Task.FromResult(RedisValue.Null);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void Read_WithNullRedisValue_DoesNotCallDeserialize()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var task = Task.FromResult(RedisValue.Null);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		_serializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	[Fact]
	public void Read_WithNullRedisValue_DoesNotLogError()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var task = Task.FromResult(RedisValue.Null);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		_logger.DidNotReceive().Log(
			LogLevel.Error,
			Arg.Any<EventId>(),
			Arg.Any<object>(),
			Arg.Any<Exception>(),
			Arg.Any<Func<object, Exception?, string>>());
	}

	[Fact]
	public void Read_WithNullRedisValue_DoesNotDeleteKey()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var task = Task.FromResult(RedisValue.Null);
		var database = Substitute.For<IDatabase>();
		_context.Database.Returns(database);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		database.DidNotReceive().KeyDelete(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
	}

	#endregion

	#region Read Tests - Deserialization Error Cases

	[Fact]
	public void Read_WithDeserializationException_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var exception = new InvalidOperationException("Deserialization failed");
		_serializer.When(x => x.Deserialize<TestEntity>(Arg.Any<byte[]>())).Do(x => throw exception);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void Read_WithDeserializationException_LogsError()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var exception = new InvalidOperationException("Deserialization failed");
		_serializer.When(x => x.Deserialize<TestEntity>(Arg.Any<byte[]>())).Do(x => throw exception);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		_logger.Received(1).Log(
			LogLevel.Error,
			Arg.Any<EventId>(),
			Arg.Is<object>(o => o.ToString()!.Contains("Error deserializing key")),
			exception,
			Arg.Any<Func<object, Exception?, string>>());
	}

	[Fact]
	public void Read_WithDeserializationException_DeletesCorruptedKey()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var exception = new InvalidOperationException("Deserialization failed");
		_serializer.When(x => x.Deserialize<TestEntity>(Arg.Any<byte[]>())).Do(x => throw exception);
		var database = Substitute.For<IDatabase>();
		_context.Database.Returns(database);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		database.Received(1).KeyDelete(key, CommandFlags.None);
	}

	[Fact]
	public void Read_WithDeserializationException_DeletesKeyWithCorrectKeyValue()
	{
		// Arrange
		var key = new RedisKey("specific:test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var exception = new InvalidOperationException("Deserialization failed");
		_serializer.When(x => x.Deserialize<TestEntity>(Arg.Any<byte[]>())).Do(x => throw exception);
		var database = Substitute.For<IDatabase>();
		_context.Database.Returns(database);

		// Act
		_reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		database.Received(1).KeyDelete(Arg.Is<RedisKey>(k => k == key), Arg.Any<CommandFlags>());
	}

	#endregion

	#region Read Tests - Different Exception Types

	[Fact]
	public void Read_WithArgumentException_ReturnsNullAndDeletesKey()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var exception = new ArgumentException("Invalid argument");
		_serializer.When(x => x.Deserialize<TestEntity>(Arg.Any<byte[]>())).Do(x => throw exception);
		var database = Substitute.For<IDatabase>();
		_context.Database.Returns(database);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.Null(result);
		database.Received(1).KeyDelete(key, CommandFlags.None);
		_logger.Received(1).Log(
			LogLevel.Error,
			Arg.Any<EventId>(),
			Arg.Any<object>(),
			exception,
			Arg.Any<Func<object, Exception?, string>>());
	}

	[Fact]
	public void Read_WithFormatException_ReturnsNullAndDeletesKey()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var exception = new FormatException("Invalid format");
		_serializer.When(x => x.Deserialize<TestEntity>(Arg.Any<byte[]>())).Do(x => throw exception);
		var database = Substitute.For<IDatabase>();
		_context.Database.Returns(database);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.Null(result);
		database.Received(1).KeyDelete(key, CommandFlags.None);
		_logger.Received(1).Log(
			LogLevel.Error,
			Arg.Any<EventId>(),
			Arg.Any<object>(),
			exception,
			Arg.Any<Func<object, Exception?, string>>());
	}

	#endregion

	#region Read Tests - Edge Cases

	[Fact]
	public void Read_WithEmptyByteArray_AttemptsDeserialization()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = Array.Empty<byte>();
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		var expectedEntity = new TestEntity { Name = "Empty", Id = 0 };
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns(expectedEntity);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		_serializer.Received(1).Deserialize<TestEntity>(Arg.Is<byte[]>(b => b.Length == 0));
	}

	[Fact]
	public void Read_WithDeserializerReturningNull_ReturnsNull()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var bytes = new byte[] { 1, 2, 3 };
		var redisValue = (RedisValue)bytes;
		var task = Task.FromResult(redisValue);
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns((TestEntity?)null);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void Read_WithLargeByteArray_DeserializesCorrectly()
	{
		// Arrange
		var key = new RedisKey("test:key");
		var largeArray = new byte[10000];
		Array.Fill(largeArray, (byte)42);
		var redisValue = (RedisValue)largeArray;
		var task = Task.FromResult(redisValue);
		var expectedEntity = new TestEntity { Name = "Large", Id = 999 };
		_serializer.Deserialize<TestEntity>(Arg.Any<byte[]>()).Returns(expectedEntity);

		// Act
		var result = _reader.Read<TestEntity>(_context, _serializer, key, task);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		_serializer.Received(1).Deserialize<TestEntity>(Arg.Is<byte[]>(b => b.Length == 10000));
	}

	#endregion
}
