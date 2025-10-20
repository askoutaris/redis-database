using RedisDatabase.Builders;
using RedisDatabase.Serializers;

namespace RedisDatabase.Builders.StreamAdapterBuilderSteps
{
	/// <summary>
	/// Second step in the stream adapter builder flow: configures the serializer for stream messages.
	/// The serializer converts message objects to byte arrays for storage in the Redis stream.
	/// </summary>
	public interface ISerializerStep<TType> where TType : class
	{
		/// <summary>
		/// Configures the serializer for encoding and decoding stream messages.
		/// </summary>
		/// <param name="serializer">The serializer implementation for message serialization.</param>
		IMaxLengthStep<TType> WithSerializer(IRedisSerializer serializer);
	}

	/// <summary>
	/// Implementation of the serializer configuration step in the stream adapter builder.
	/// </summary>
	/// <typeparam name="TType">The type of messages in the stream.</typeparam>
	public class SerializerStep<TType> : ISerializerStep<TType>
		where TType : class
	{
		private readonly IStreamAdapterBuilder<TType> _builder;

		/// <summary>
		/// Initializes a new instance of the serializer step with the specified builder.
		/// </summary>
		/// <param name="builder">The stream adapter builder to configure.</param>
		public SerializerStep(IStreamAdapterBuilder<TType> builder)
		{
			_builder = builder;
		}

		/// <inheritdoc/>
		public IMaxLengthStep<TType> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_builder.WithSerializer(serializer);

			return new MaxLengthStep<TType>(_builder);
		}
	}
}
