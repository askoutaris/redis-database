using RedisDatabase.Extensions;
using RedisDatabase.Serializers;
using StackExchange.Redis;

namespace RedisDatabase.Adapters
{
	/// <summary>
	/// Provides Redis Streams operations with consumer group support, serialization, and direct database access.
	/// </summary>
	/// <typeparam name="TType">The message type for stream entries.</typeparam>
	public interface IStreamAdapter<TType> where TType : class
	{
		/// <summary>
		/// Queues a command to add a serialized message to the Redis stream with approximate max-length capping.
		/// </summary>
		/// <param name="message">The message to serialize and add to the stream.</param>
		void Add(TType message);

		/// <summary>
		/// Reads pending and new messages from the stream for a consumer group, handling capped messages and ensuring group existence.
		/// Reads up to <paramref name="pageSize"/> messages, prioritizing pending messages first, then new messages.
		/// </summary>
		/// <param name="groupName">The consumer group name.</param>
		/// <param name="consumerName">The consumer name within the group.</param>
		/// <param name="pageSize">Maximum number of messages to read.</param>
		/// <returns>An array of deserialized <see cref="StreamItem{TType}"/> ordered by message ID.</returns>
		Task<StreamItem<TType>[]> Read(string groupName, string consumerName, int pageSize);

		/// <summary>
		/// Queues a command to acknowledge messages for a consumer group, removing them from the pending entries list.
		/// </summary>
		/// <param name="groupName">The consumer group name.</param>
		/// <param name="messageIds">The collection of message IDs to acknowledge.</param>
		void Acknowledge(string groupName, ICollection<string> messageIds);
	}

	class StreamAdapter<TType> : IStreamAdapter<TType>
		where TType : class
	{
		private const string _fieldName = "bytes";

		private readonly IRedisContext _context;
		private readonly IRedisSerializer _serializer;
		private readonly string _streamKey;
		private readonly int _maxLength;
		private readonly ILogger _logger;

		public StreamAdapter(IRedisContext context, IRedisSerializer serializer, string streamKey, int maxLength, ILogger logger)
		{
			_context = context;
			_serializer = serializer;
			_streamKey = streamKey;
			_maxLength = maxLength > 0 ? maxLength : throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength must be greater than 0");
			_logger = logger;
		}

		public void Add(TType message)
		{
			_logger.LogDebug("Adding message to stream {StreamKey} with maxLength {MaxLength} message {message}", _streamKey, _maxLength, message.ToLogData());

			_context.AddCommand((db) => db.StreamAddAsync(
					key: _streamKey,
					streamField: _fieldName,
					streamValue: _serializer.Serialize(message),
					maxLength: _maxLength,
					useApproximateMaxLength: true
				));

			_logger.LogTrace("Successfully queued stream add command for {StreamKey}", _streamKey);
		}

		public async Task<StreamItem<TType>[]> Read(string groupName, string consumerName, int pageSize)
		{
			_logger.LogDebug("Reading from stream {StreamKey} - Group: {GroupName}, Consumer: {ConsumerName}, PageSize: {PageSize}", _streamKey, groupName, consumerName, pageSize);

			await EnsureGroup(groupName);

			_logger.LogTrace("Reading pending messages from stream {StreamKey} for group {GroupName}", _streamKey, groupName);
			var values = await _context.Database.StreamReadGroupAsync(_streamKey, groupName, consumerName, StreamConstants.AllMessages, pageSize);

			_logger.LogTrace("Retrieved {Count} pending messages from stream {StreamKey}", values.Length, _streamKey);

			var remainingCount = pageSize - values.Length;

			if (remainingCount > 0)
			{
				_logger.LogDebug("Reading {RemainingCount} new messages from stream {StreamKey}", remainingCount, _streamKey);
				var newValues = await _context.Database.StreamReadGroupAsync(_streamKey, groupName, consumerName, StreamConstants.UndeliveredMessages, remainingCount);

				_logger.LogTrace("Retrieved {Count} new messages from stream {StreamKey}", newValues.Length, _streamKey);
				values = [.. values.Concat(newValues).OrderBy(x => x.Id)];
			}

			var entries = DeserializeEntries(values, out var cappedMessageIds);

			if (cappedMessageIds.Count != 0)
			{
				_logger.LogWarning("Found {CappedCount} capped/missing messages in stream {StreamKey}", cappedMessageIds.Count, _streamKey);

				var ids = cappedMessageIds
					.Select(x => (RedisValue)x)
					.ToArray();

				_context.Database.StreamAcknowledge(_streamKey, groupName, ids);

				_logger.LogDebug("Successfully cleaned up {Count} capped messages for group {GroupName}", cappedMessageIds.Count, groupName);
			}

			_logger.LogTrace("Successfully read and deserialized {ValidCount} valid entries from stream {StreamKey}", entries.Count, _streamKey);
			return [.. entries];
		}

		public void Acknowledge(string groupName, ICollection<string> messageIds)
		{
			_logger.LogTrace("Acknowledging {Count} messages for group {GroupName} in stream {StreamKey}", messageIds.Count, groupName, _streamKey);

			if (messageIds.Count == 0)
			{
				_logger.LogTrace("No messages to acknowledge for group {GroupName} in stream {StreamKey}", groupName, _streamKey);
				return;
			}

			var ids = messageIds
				.Select(x => (RedisValue)x)
				.ToArray();

			_logger.LogDebug("Acknowledging message IDs: {MessageIds} for group {GroupName} in stream {StreamKey}", messageIds.ToLogData(), groupName, _streamKey);

			_context.AddCommand(db => db.StreamAcknowledgeAsync(_streamKey, groupName, ids).ContinueWith(task =>
			{
				var acknowledgedCount = task.Result;

				_logger.LogTrace("Successfully acknowledged {AcknowledgedCount} out of {RequestedCount} messages for group {GroupName}", acknowledgedCount, messageIds.Count, groupName);

				if (acknowledgedCount != messageIds.Count)
					_logger.LogWarning("Acknowledged {AcknowledgedCount} messages but requested {RequestedCount} for group {GroupName} in stream {StreamKey}", acknowledgedCount, messageIds.Count, groupName, _streamKey);
			}));
		}

		private async Task EnsureGroup(string groupName)
		{
			_logger.LogTrace("Ensuring consumer group {GroupName} exists for stream {StreamKey}", groupName, _streamKey);

			try
			{
				var groups = await _context.Database.StreamGroupInfoAsync(_streamKey);
				_logger.LogTrace("Found {GroupCount} existing groups for stream {StreamKey}", groups.Length, _streamKey);

				if (!groups.Any(x => x.Name == groupName))
				{
					_logger.LogDebug("Creating consumer group {GroupName} for stream {StreamKey}", groupName, _streamKey);
					await _context.Database.StreamCreateConsumerGroupAsync(_streamKey, groupName, StreamConstants.AllMessages);
					_logger.LogDebug("Successfully created consumer group {GroupName} for stream {StreamKey}", groupName, _streamKey);
				}
				else
				{
					_logger.LogTrace("Consumer group {GroupName} already exists for stream {StreamKey}", groupName, _streamKey);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to ensure consumer group {GroupName} for stream {StreamKey}", groupName, _streamKey);
				throw;
			}
		}

		private List<StreamItem<TType>> DeserializeEntries(StreamEntry[] values, out List<string> cappedMessageIds)
		{
			_logger.LogDebug("Deserializing {TotalCount} messages from stream {StreamKey}", values.Length, _streamKey);

			cappedMessageIds = [];
			var entries = new List<StreamItem<TType>>(values.Length);

			foreach (var value in values)
			{
				try
				{
					// in case we have too old pending messages and stream is getting capped
					// then these message ids brings null values (or empty array values)
					// this is not a normal case thoughs... means that something went wrong and we didn't consume these messages on time
					var fieldValue = value[_fieldName];

					if (fieldValue.IsNullOrEmpty)
					{
						_logger.LogDebug("Found capped/missing message with ID {MessageId} in stream", value.Id);
						cappedMessageIds.Add(value.Id!);
						continue;
					}

					var message = _serializer.Deserialize<TType>(fieldValue!);

					if (message is not null)
					{
						entries.Add(new StreamItem<TType>(value.Id!, message));
						_logger.LogTrace("Successfully deserialized message {MessageId}", value.Id);
					}
					else
					{
						_logger.LogWarning("Deserialization returned null for message {MessageId}", value.Id);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to deserialize message {MessageId} from stream {StreamKey}", value.Id, _streamKey);
				}
			}

			_logger.LogTrace("Completed deserialization: {ValidCount} valid entries, {CappedCount} capped messages", entries.Count, cappedMessageIds.Count);

			return entries;
		}
	}
}
