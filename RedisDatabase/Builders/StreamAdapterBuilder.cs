using RedisDatabase.Adapters;
using RedisDatabase.Serializers;

namespace RedisDatabase.Builders
{
	/// <summary>
	/// Fluent builder for configuring <see cref="IStreamAdapter{TType}"/> instances with Redis Streams.
	/// </summary>
	/// <typeparam name="TType">The message type for stream entries.</typeparam>
	public interface IStreamAdapterBuilder<TType> : ICollectionBuilder
		where TType : class
	{
		/// <summary>Configures the Redis stream key name.</summary>
		IStreamAdapterBuilder<TType> WithStreamKey(string streamKey);

		/// <summary>Configures the serializer for message serialization/deserialization.</summary>
		IStreamAdapterBuilder<TType> WithSerializer(IRedisSerializer serializer);

		/// <summary>Configures the approximate maximum stream length for automatic capping.</summary>
		IStreamAdapterBuilder<TType> WithMaxLength(int maxLength);

		/// <summary>Builds the stream adapter instance with the specified Redis context.</summary>
		internal IStreamAdapter<TType> Build(IRedisContext context);
	}

	class StreamAdapterBuilder<TType> : IStreamAdapterBuilder<TType>
		where TType : class
	{
		private string? _streamKey;
		private IRedisSerializer? _serializer;
		private int? _maxLength;
		private ILogger<StreamAdapter<TType>> _logger;

		public StreamAdapterBuilder(ILoggerFactory loggerFactory)
		{
			_logger = loggerFactory.CreateLogger<StreamAdapter<TType>>();
		}

		public IStreamAdapterBuilder<TType> WithStreamKey(string streamKey)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(streamKey, nameof(streamKey));

			_streamKey = streamKey;

			return this;
		}

		public IStreamAdapterBuilder<TType> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_serializer = serializer;

			return this;
		}

		public IStreamAdapterBuilder<TType> WithMaxLength(int maxLength)
		{
			if (maxLength <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength must be greater than 0");

			_maxLength = maxLength;

			return this;
		}

		IStreamAdapter<TType> IStreamAdapterBuilder<TType>.Build(IRedisContext context)
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			if (_streamKey is null)
				throw new InvalidOperationException("StreamKey is required. Call WithStreamKey before Build.");

			if (_serializer is null)
				throw new InvalidOperationException("Serializer is required. Call WithSerializer before Build.");

			if (_maxLength is null)
				throw new InvalidOperationException("MaxLength is required. Call WithMaxLength before Build.");

			return new StreamAdapter<TType>(
				context: context,
				serializer: _serializer,
				streamKey: _streamKey,
				maxLength: _maxLength.Value,
				logger: _logger);
		}
	}
}
