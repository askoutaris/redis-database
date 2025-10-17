namespace RedisDatabase.Serializers.RedisSerializerSignatureDecorators
{
	[Serializable]
	public class CacheEntry
	{
		public byte[]? Bytes { get; }
		public string Signature { get; }

		public CacheEntry(byte[]? bytes, string signature)
		{
			Bytes = bytes;
			Signature = signature;
		}
	}
}
