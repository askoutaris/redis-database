namespace RedisDatabase.Serializers.RedisSerializerSignatureDecorators
{
	class MismatchSignatureException : Exception
	{
		public MismatchSignatureException(string? message) : base(message)
		{

		}
	}
}
