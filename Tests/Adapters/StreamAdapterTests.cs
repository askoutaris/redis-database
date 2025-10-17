using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Serializers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;

namespace Tests.Adapters;

public class StreamAdapterTests
{
	private readonly IRedisContext _redisContext;
	private readonly IRedisSerializer _serializer;
	private readonly ILogger _logger;
	private readonly IDatabase _database;
	private readonly StreamAdapter<TestMessage> _adapter;

	public StreamAdapterTests()
	{
		_redisContext = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
		_logger = Substitute.For<ILogger>();
		_database = Substitute.For<IDatabase>();
		_redisContext.Database.Returns(_database);

		_adapter = new StreamAdapter<TestMessage>(
			_redisContext,
			_serializer,
			"test:stream",
			1000,
			_logger);
	}

	private class TestMessage
	{
		public string Content { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var adapter = new StreamAdapter<TestMessage>(
			_redisContext,
			_serializer,
			"custom:stream",
			500,
			_logger);

		Assert.NotNull(adapter);
	}

	[Fact]
	public void Constructor_WithZeroMaxLength_ThrowsArgumentOutOfRangeException()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			new StreamAdapter<TestMessage>(_redisContext, _serializer, "stream", 0, _logger));

		Assert.Equal("maxLength", exception.ParamName);
		Assert.Contains("MaxLength must be greater than 0", exception.Message);
	}

	[Fact]
	public void Constructor_WithNegativeMaxLength_ThrowsArgumentOutOfRangeException()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			new StreamAdapter<TestMessage>(_redisContext, _serializer, "stream", -1, _logger));

		Assert.Equal("maxLength", exception.ParamName);
	}
	#endregion

	#region Add Tests

	[Fact]
	public void Add_WithValidMessage_AddsCommandToContext()
	{
		var message = new TestMessage { Content = "test", Id = 1 };

		_adapter.Add(message);

		// Add method uses deferred execution, so only AddCommand should be called immediately
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Add_WithNullMessage_AddsCommandToContext()
	{
		_adapter.Add(null!);

		// Add method uses deferred execution, so only AddCommand should be called immediately
		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	#endregion

	#region Read Tests

	[Fact]
	public async Task Read_WithValidParameters_ReadsAndDeserializesMessages()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		// Setup stream entries
		var pendingEntries = new StreamEntry[]
		{
			new("1234-0", [new NameValueEntry("bytes", "serialized-data-1")])
		};

		var newEntries = new StreamEntry[]
		{
			new("1235-0", [new NameValueEntry("bytes", "serialized-data-2")])
		};

		// Mock database responses
		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(Array.Empty<StreamGroupInfo>()));

		_database.StreamCreateConsumerGroupAsync("test:stream", groupName, StreamConstants.AllMessages)
			.Returns(Task.FromResult(true));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.AllMessages, pageSize)
			.Returns(Task.FromResult(pendingEntries));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.UndeliveredMessages, pageSize - 1)
			.Returns(Task.FromResult(newEntries));

		// Mock serializer
		var message1 = new TestMessage { Content = "test1", Id = 1 };
		var message2 = new TestMessage { Content = "test2", Id = 2 };
		_serializer.Deserialize<TestMessage>(Arg.Any<byte[]>()).Returns(message1, message2);

		var result = await _adapter.Read(groupName, consumerName, pageSize);

		Assert.Equal(2, result.Length);
		Assert.Equal("1234-0", result[0].Id);
		Assert.Equal("test1", result[0].Value.Content);
		Assert.Equal("1235-0", result[1].Id);
		Assert.Equal("test2", result[1].Value.Content);
	}

	[Fact]
	public async Task Read_WithCappedMessages_CleansUpCappedMessages()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		// Setup stream entries with capped message
		var pendingEntries = new StreamEntry[]
		{
			new("1234-0", [new NameValueEntry("bytes", "serialized-data")]),
			new("1235-0", [new NameValueEntry("bytes", RedisValue.Null)]) // Capped message
		};

		// Mock database responses
		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(Array.Empty<StreamGroupInfo>()));

		_database.StreamCreateConsumerGroupAsync("test:stream", groupName, StreamConstants.AllMessages)
			.Returns(Task.FromResult(true));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.AllMessages, pageSize)
			.Returns(Task.FromResult(pendingEntries));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.UndeliveredMessages, pageSize - 2)
			.Returns(Task.FromResult(Array.Empty<StreamEntry>()));

		// Mock serializer
		var message = new TestMessage { Content = "test", Id = 1 };
		_serializer.Deserialize<TestMessage>(Arg.Any<byte[]>()).Returns(message);

		var result = await _adapter.Read(groupName, consumerName, pageSize);

		Assert.Single(result);
		Assert.Equal("1234-0", result[0].Id);
		Assert.Equal("test", result[0].Value.Content);

		// Verify cleanup was called (synchronous acknowledgment via Database.StreamAcknowledge)
		_database.Received(1).StreamAcknowledge("test:stream", groupName,
			Arg.Is<RedisValue[]>(ids => ids.Length == 1 && ids[0] == "1235-0"));
	}

	[Fact]
	public async Task Read_WithExistingGroup_DoesNotCreateGroup()
	{
		var groupName = "existing-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		// Create existing group info using our unsafe helper method
		var existingGroupInfo = CreateStreamGroupInfo(groupName, 5, 2, 100);
		var groups = new[] { existingGroupInfo };

		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(groups));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.AllMessages, pageSize)
			.Returns(Task.FromResult(Array.Empty<StreamEntry>()));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.UndeliveredMessages, pageSize)
			.Returns(Task.FromResult(Array.Empty<StreamEntry>()));

		var result = await _adapter.Read(groupName, consumerName, pageSize);

		// Verify that StreamGroupInfoAsync was called
		await _database.Received(1).StreamGroupInfoAsync("test:stream");
		// Verify that StreamCreateConsumerGroupAsync was NOT called since group exists
		await _database.DidNotReceive().StreamCreateConsumerGroupAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<RedisValue?>());

		Assert.Empty(result);
	}

	[Fact]
	public async Task Read_WithDeserializationErrors_SkipsFailedMessages()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		var entries = new StreamEntry[]
		{
			new("1234-0", [new NameValueEntry("bytes", "valid-data")]),
			new("1235-0", [new NameValueEntry("bytes", "invalid-data")])
		};

		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(Array.Empty<StreamGroupInfo>()));

		_database.StreamCreateConsumerGroupAsync("test:stream", groupName, StreamConstants.AllMessages)
			.Returns(Task.FromResult(true));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.AllMessages, pageSize)
			.Returns(Task.FromResult(entries));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.UndeliveredMessages, pageSize - 2)
			.Returns(Task.FromResult(Array.Empty<StreamEntry>()));

		// Mock serializer to return success for first, exception for second
		var message = new TestMessage { Content = "test", Id = 1 };
		var callCount = 0;
		_serializer.Deserialize<TestMessage>(Arg.Any<byte[]>()).Returns(x =>
		{
			callCount++;
			if (callCount == 1) return message;
			throw new InvalidOperationException("Deserialization failed");
		});

		var result = await _adapter.Read(groupName, consumerName, pageSize);

		Assert.Single(result);
		Assert.Equal("1234-0", result[0].Id);
		Assert.Equal("test", result[0].Value.Content);
	}

	[Fact]
	public async Task Read_WhenStreamGroupInfoThrows_PropagatesException()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromException<StreamGroupInfo[]>(new RedisException("Stream does not exist")));

		await Assert.ThrowsAsync<RedisException>(() => _adapter.Read(groupName, consumerName, pageSize));
	}

	[Fact]
	public async Task Read_WhenCreateGroupThrows_PropagatesException()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(Array.Empty<StreamGroupInfo>()));

		_database.StreamCreateConsumerGroupAsync("test:stream", groupName, StreamConstants.AllMessages)
			.Returns(Task.FromException<bool>(new RedisException("Permission denied")));

		await Assert.ThrowsAsync<RedisException>(() => _adapter.Read(groupName, consumerName, pageSize));
	}

	[Fact]
	public async Task Read_WithNullDeserializationResult_SkipsMessage()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		var entries = new StreamEntry[]
		{
			new("1234-0", [new NameValueEntry("bytes", "valid-data")]),
			new("1235-0", [new NameValueEntry("bytes", "invalid-data")])
		};

		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(Array.Empty<StreamGroupInfo>()));

		_database.StreamCreateConsumerGroupAsync("test:stream", groupName, StreamConstants.AllMessages)
			.Returns(Task.FromResult(true));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.AllMessages, pageSize)
			.Returns(Task.FromResult(entries));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.UndeliveredMessages, pageSize - 2)
			.Returns(Task.FromResult(Array.Empty<StreamEntry>()));

		var message = new TestMessage { Content = "test", Id = 1 };
		_serializer.Deserialize<TestMessage>(Arg.Any<byte[]>()).Returns(message, (TestMessage)null!);

		var result = await _adapter.Read(groupName, consumerName, pageSize);

		Assert.Single(result);
		Assert.Equal("1234-0", result[0].Id);
		Assert.Equal("test", result[0].Value.Content);
	}

	[Fact]
	public async Task Read_WithEmptyFieldValue_TreatAsCappeMessage()
	{
		var groupName = "test-group";
		var consumerName = "test-consumer";
		var pageSize = 10;

		// Setup stream entries with empty field value
		var entries = new StreamEntry[]
		{
			new("1234-0", [new NameValueEntry("bytes", "serialized-data")]),
			new("1235-0", [new NameValueEntry("bytes", "")]) // Empty field value
		};

		_database.StreamGroupInfoAsync("test:stream")
			.Returns(Task.FromResult(Array.Empty<StreamGroupInfo>()));

		_database.StreamCreateConsumerGroupAsync("test:stream", groupName, StreamConstants.AllMessages)
			.Returns(Task.FromResult(true));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.AllMessages, pageSize)
			.Returns(Task.FromResult(entries));

		_database.StreamReadGroupAsync("test:stream", groupName, consumerName, StreamConstants.UndeliveredMessages, pageSize - 2)
			.Returns(Task.FromResult(Array.Empty<StreamEntry>()));

		var message = new TestMessage { Content = "test", Id = 1 };
		_serializer.Deserialize<TestMessage>(Arg.Any<byte[]>()).Returns(message);

		var result = await _adapter.Read(groupName, consumerName, pageSize);

		Assert.Single(result);
		Assert.Equal("1234-0", result[0].Id);
		Assert.Equal("test", result[0].Value.Content);

		// Verify cleanup was called for empty field message (synchronous acknowledgment)
		_database.Received(1).StreamAcknowledge("test:stream", groupName,
			Arg.Is<RedisValue[]>(ids => ids.Length == 1 && ids[0] == "1235-0"));
	}

	#endregion

	#region Acknowledge Tests

	[Fact]
	public void Acknowledge_WithValidMessageIds_AddsCommandToContext()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0", "1235-0" };

		_adapter.Acknowledge(groupName, messageIds);

		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Acknowledge_WithEmptyCollection_DoesNotAddCommand()
	{
		var groupName = "test-group";
		var messageIds = Array.Empty<string>();

		_adapter.Acknowledge(groupName, messageIds);

		_redisContext.DidNotReceive().AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Acknowledge_WithSingleMessageId_AddsCommandToContext()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0" };

		_adapter.Acknowledge(groupName, messageIds);

		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void Acknowledge_WithMultipleMessageIds_AddsCommandToContext()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0", "1235-0", "1236-0" };

		_adapter.Acknowledge(groupName, messageIds);

		_redisContext.Received(1).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public async Task Acknowledge_ExecutedCommand_CallsStreamAcknowledgeAsync()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0", "1235-0" };
		Func<IDatabaseAsync, Task>? capturedCommand = null;

		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(cmd => capturedCommand = cmd));
		_database.StreamAcknowledgeAsync("test:stream", groupName, Arg.Any<RedisValue[]>())
			.Returns(Task.FromResult(2L));

		_adapter.Acknowledge(groupName, messageIds);

		Assert.NotNull(capturedCommand);
		await capturedCommand(_database);

		await _database.Received(1).StreamAcknowledgeAsync("test:stream", groupName,
			Arg.Is<RedisValue[]>(ids => ids.Length == 2 && ids[0] == "1234-0" && ids[1] == "1235-0"));
	}

	[Fact]
	public async Task Acknowledge_ExecutedCommand_WithPartialAcknowledgment_LogsWarning()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0", "1235-0", "1236-0" };
		Func<IDatabaseAsync, Task>? capturedCommand = null;

		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(cmd => capturedCommand = cmd));
		_database.StreamAcknowledgeAsync("test:stream", groupName, Arg.Any<RedisValue[]>())
			.Returns(Task.FromResult(2L)); // Only 2 out of 3 acknowledged

		_adapter.Acknowledge(groupName, messageIds);

		Assert.NotNull(capturedCommand);
		await capturedCommand(_database);

		_logger.Received(1).Log(
			LogLevel.Warning,
			Arg.Any<EventId>(),
			Arg.Is<object>(o => o.ToString()!.Contains("Acknowledged 2 messages but requested 3")),
			null,
			Arg.Any<Func<object, Exception?, string>>());
	}

	[Fact]
	public async Task Acknowledge_ExecutedCommand_WithFullAcknowledgment_DoesNotLogWarning()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0", "1235-0" };
		Func<IDatabaseAsync, Task>? capturedCommand = null;

		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(cmd => capturedCommand = cmd));
		_database.StreamAcknowledgeAsync("test:stream", groupName, Arg.Any<RedisValue[]>())
			.Returns(Task.FromResult(2L)); // All 2 acknowledged

		_adapter.Acknowledge(groupName, messageIds);

		Assert.NotNull(capturedCommand);
		await capturedCommand(_database);

		_logger.DidNotReceive().Log(
			LogLevel.Warning,
			Arg.Any<EventId>(),
			Arg.Any<object>(),
			null,
			Arg.Any<Func<object, Exception?, string>>());
	}

	[Fact]
	public async Task Acknowledge_ExecutedCommand_LogsTraceOnSuccess()
	{
		var groupName = "test-group";
		var messageIds = new[] { "1234-0", "1235-0" };
		Func<IDatabaseAsync, Task>? capturedCommand = null;

		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(cmd => capturedCommand = cmd));
		_database.StreamAcknowledgeAsync("test:stream", groupName, Arg.Any<RedisValue[]>())
			.Returns(Task.FromResult(2L));

		_adapter.Acknowledge(groupName, messageIds);

		Assert.NotNull(capturedCommand);
		await capturedCommand(_database);

		_logger.Received(1).Log(
			LogLevel.Trace,
			Arg.Any<EventId>(),
			Arg.Is<object>(o => o.ToString()!.Contains("Successfully acknowledged 2 out of 2")),
			null,
			Arg.Any<Func<object, Exception?, string>>());
	}

	#endregion

	// Helper method to create StreamGroupInfo using unsafe code (moved from StreamAdapterOperationsTests)
	private static unsafe StreamGroupInfo CreateStreamGroupInfo(string name, int consumerCount, long pendingMessageCount, long lastDeliveredId)
	{
		// Create an uninitialized StreamGroupInfo
		var streamGroupInfo = new StreamGroupInfo();

		// Use unsafe code to directly set the first field (likely the name)
		var redisValue = (RedisValue)name;

		// Get a pointer to the struct
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
		StreamGroupInfo* ptr = &streamGroupInfo;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

		// Cast to byte pointer to manipulate memory directly
		byte* bytePtr = (byte*)ptr;

		// Copy the RedisValue bytes to the beginning of the struct
		// RedisValue is likely the first field
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
		RedisValue* namePtr = (RedisValue*)bytePtr;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
		*namePtr = redisValue;

		return streamGroupInfo;
	}
}