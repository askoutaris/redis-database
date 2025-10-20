namespace RedisDatabase.Serializers.RedisSerializerSignatureDecorators
{
	/// <summary>
	/// Represents a cached entry with serialized bytes and type signature for schema validation.
	/// </summary>
	[Serializable]
	public class CacheEntry
	{
		/// <summary>
		/// Gets the serialized entity bytes.
		/// </summary>
		public byte[]? Bytes { get; }

		/// <summary>
		/// Gets the type signature hash for schema change detection.
		/// </summary>
		public string Signature { get; }

		/// <summary>
		/// Initializes a new cache entry with the specified bytes and signature.
		/// </summary>
		/// <param name="bytes">The serialized entity bytes.</param>
		/// <param name="signature">The type signature hash.</param>
		public CacheEntry(byte[]? bytes, string signature)
		{
			Bytes = bytes;
			Signature = signature;
		}
	}
}
