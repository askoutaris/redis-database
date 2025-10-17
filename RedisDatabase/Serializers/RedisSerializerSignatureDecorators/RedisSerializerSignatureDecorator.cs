namespace RedisDatabase.Serializers.RedisSerializerSignatureDecorators
{
	class RedisSerializerSignatureDecorator : IRedisSerializer
	{
		private readonly IRedisSerializer _serializer;
		private readonly ITypeSignatureProvider _signatureProvider;

		public RedisSerializerSignatureDecorator(IRedisSerializer serializer, ITypeSignatureProvider signatureProvider)
		{
			_serializer = serializer;
			_signatureProvider = signatureProvider;
		}

		public TType? Deserialize<TType>(byte[] bytes)
		{
			var cacheEntry = _serializer.Deserialize<CacheEntry>(bytes);

			if (cacheEntry is null || cacheEntry.Bytes is null)
				return default;

			var signature = _signatureProvider.GetSignature(typeof(TType));

			if (cacheEntry.Signature != signature)
				throw new MismatchSignatureException($"Mismatched type signature detected for {typeof(TType).FullName}. The cached data structure may have changed.");

			var item = _serializer.Deserialize<TType>(cacheEntry.Bytes);

			return item;
		}

		public byte[] Serialize<TType>(TType value)
		{
			var bytes = _serializer.Serialize(value);

			var signature = _signatureProvider.GetSignature(typeof(TType));

			var cacheEntry = new CacheEntry(bytes, signature);

			return _serializer.Serialize(cacheEntry);
		}
	}
}
