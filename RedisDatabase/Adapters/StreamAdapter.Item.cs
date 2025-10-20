using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Adapters
{
	/// <summary>
	/// Represents an item from a Redis stream with its unique ID and typed value.
	/// </summary>
	/// <typeparam name="TType">The type of the stream value.</typeparam>
	[ExcludeFromCodeCoverage]
	public readonly struct StreamItem<TType>
	{
		/// <summary>
		/// Gets the unique stream entry ID assigned by Redis.
		/// </summary>
		public string Id { get; }

		/// <summary>
		/// Gets the deserialized value of the stream entry.
		/// </summary>
		public TType Value { get; }

		/// <summary>
		/// Initializes a new stream item with the specified ID and value.
		/// </summary>
		/// <param name="id">The unique stream entry ID.</param>
		/// <param name="value">The stream entry value.</param>
		public StreamItem(string id, TType value)
		{
			Id = id;
			Value = value;
		}
	}
}
