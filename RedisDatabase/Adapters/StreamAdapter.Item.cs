using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.Adapters
{
	[ExcludeFromCodeCoverage]
	public readonly struct StreamItem<TType>
	{
		public string Id { get; }
		public TType Value { get; }

		public StreamItem(string id, TType value)
		{
			Id = id;
			Value = value;
		}
	}
}
