using RedisDatabase.Builders;

namespace RedisDatabase.Builders.StreamAdapterBuilderSteps
{
	/// <summary>
	/// Final step in the stream adapter builder flow: configures the maximum number of messages to retain in the stream.
	/// Redis will automatically trim older messages when this limit is exceeded using the MAXLEN ~ approximation.
	/// </summary>
	public interface IMaxLengthStep<TType> where TType : class
	{
		/// <summary>
		/// Configures the approximate maximum number of messages to keep in the stream.
		/// Older messages are automatically trimmed when adding new messages exceeds this limit.
		/// Example: maxLength of 10000 keeps approximately the most recent 10,000 messages.
		/// </summary>
		/// <param name="maxLength">The approximate maximum number of messages to retain (must be greater than 0).</param>
		IStreamAdapterBuilder<TType> WithMaxLength(int maxLength);
	}

	public class MaxLengthStep<TType> : IMaxLengthStep<TType>
		where TType : class
	{
		private readonly IStreamAdapterBuilder<TType> _builder;

		public MaxLengthStep(IStreamAdapterBuilder<TType> builder)
		{
			_builder = builder;
		}

		public IStreamAdapterBuilder<TType> WithMaxLength(int maxLength)
		{
			if (maxLength <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength must be greater than 0");

			_builder.WithMaxLength(maxLength);

			return _builder;
		}
	}
}
