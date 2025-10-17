using System.Collections.Concurrent;
using TypeSignature;

namespace RedisDatabase.Serializers.RedisSerializerSignatureDecorators
{
	interface ITypeSignatureProvider
	{
		string GetSignature(Type type);
	}

	class TypeSignatureProvider : ITypeSignatureProvider
	{
		private readonly ISignatureBuilder _signatureBuilder;
		private readonly ConcurrentDictionary<Type, string> _signatures;

		public TypeSignatureProvider(ISignatureBuilder signatureBuilder)
		{
			_signatureBuilder = signatureBuilder;
			_signatures = [];
		}

		public string GetSignature(Type type)
			=> _signatures.GetOrAdd(type, (_) => _signatureBuilder.GetSignature(type));
	}
}
