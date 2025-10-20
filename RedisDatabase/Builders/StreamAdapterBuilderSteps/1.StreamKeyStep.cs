using RedisDatabase.Builders;

namespace RedisDatabase.Builders.StreamAdapterBuilderSteps
{
	/// <summary>
	/// First step in the stream adapter builder flow: configures the Redis stream key name.
	/// This key identifies the Redis stream where messages will be published and consumed.
	/// </summary>
	public interface IStreamKeyStep<TType> where TType : class
	{
		/// <summary>
		/// Configures the Redis stream key name.
		/// Example: "order-events" creates a Redis stream at key "order-events".
		/// </summary>
		/// <param name="streamKey">The Redis stream key name.</param>
		ISerializerStep<TType> WithStreamKey(string streamKey);
	}

	/// <summary>
	/// Implementation of the stream key configuration step in the stream adapter builder.
	/// </summary>
	/// <typeparam name="TType">The type of messages in the stream.</typeparam>
	public class StreamKeyStep<TType> : IStreamKeyStep<TType>
		where TType : class
	{
		private readonly IStreamAdapterBuilder<TType> _builder;

		/// <summary>
		/// Initializes a new instance of the stream key step with the specified builder.
		/// </summary>
		/// <param name="builder">The stream adapter builder to configure.</param>
		public StreamKeyStep(IStreamAdapterBuilder<TType> builder)
		{
			_builder = builder;
		}

		/// <inheritdoc/>
		public ISerializerStep<TType> WithStreamKey(string streamKey)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(streamKey, nameof(streamKey));

			_builder.WithStreamKey(streamKey);

			return new SerializerStep<TType>(_builder);
		}
	}
}
