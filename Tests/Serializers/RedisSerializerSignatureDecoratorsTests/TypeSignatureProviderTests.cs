using RedisDatabase.Serializers.RedisSerializerSignatureDecorators;
using NSubstitute;
using TypeSignature;

namespace RedisDatabaseTests.Serializers.RedisSerializerSignatureDecoratorsTests;

public class TypeSignatureProviderTests
{
	private readonly ISignatureBuilder _signatureBuilder;
	private readonly TypeSignatureProvider _provider;

	public TypeSignatureProviderTests()
	{
		_signatureBuilder = Substitute.For<ISignatureBuilder>();
		_provider = new TypeSignatureProvider(_signatureBuilder);
	}

	public class TestEntity
	{
		public int Id { get; set; }
	}

	[Fact]
	public void GetSignature_WithValidType_ReturnsSignature()
	{
		var expectedSignature = "test-signature";
		_signatureBuilder.GetSignature(typeof(TestEntity)).Returns(expectedSignature);

		var signature = _provider.GetSignature(typeof(TestEntity));

		Assert.Equal(expectedSignature, signature);
		_signatureBuilder.Received(1).GetSignature(typeof(TestEntity));
	}

	[Fact]
	public void GetSignature_CalledTwiceWithSameType_CallsBuilderOnce()
	{
		var expectedSignature = "test-signature";
		_signatureBuilder.GetSignature(typeof(TestEntity)).Returns(expectedSignature);

		var signature1 = _provider.GetSignature(typeof(TestEntity));
		var signature2 = _provider.GetSignature(typeof(TestEntity));

		Assert.Equal(expectedSignature, signature1);
		Assert.Equal(expectedSignature, signature2);
		_signatureBuilder.Received(1).GetSignature(typeof(TestEntity));
	}

	[Fact]
	public void GetSignature_WithDifferentTypes_CallsBuilderForEach()
	{
		_signatureBuilder.GetSignature(typeof(TestEntity)).Returns("signature1");
		_signatureBuilder.GetSignature(typeof(string)).Returns("signature2");

		var signature1 = _provider.GetSignature(typeof(TestEntity));
		var signature2 = _provider.GetSignature(typeof(string));

		Assert.Equal("signature1", signature1);
		Assert.Equal("signature2", signature2);
		_signatureBuilder.Received(1).GetSignature(typeof(TestEntity));
		_signatureBuilder.Received(1).GetSignature(typeof(string));
	}

	[Fact]
	public void GetSignature_CachesResults()
	{
		var expectedSignature = "cached-signature";
		_signatureBuilder.GetSignature(typeof(TestEntity)).Returns(expectedSignature);

		_provider.GetSignature(typeof(TestEntity));
		_provider.GetSignature(typeof(TestEntity));
		_provider.GetSignature(typeof(TestEntity));

		_signatureBuilder.Received(1).GetSignature(typeof(TestEntity));
	}

	[Fact]
	public void GetSignature_WithMultipleTypes_CachesEachSeparately()
	{
		_signatureBuilder.GetSignature(typeof(TestEntity)).Returns("sig1");
		_signatureBuilder.GetSignature(typeof(string)).Returns("sig2");
		_signatureBuilder.GetSignature(typeof(int)).Returns("sig3");

		_provider.GetSignature(typeof(TestEntity));
		_provider.GetSignature(typeof(string));
		_provider.GetSignature(typeof(int));
		_provider.GetSignature(typeof(TestEntity));
		_provider.GetSignature(typeof(string));

		_signatureBuilder.Received(1).GetSignature(typeof(TestEntity));
		_signatureBuilder.Received(1).GetSignature(typeof(string));
		_signatureBuilder.Received(1).GetSignature(typeof(int));
	}

	[Fact]
	public void Constructor_WithSignatureBuilder_CreatesInstance()
	{
		var provider = new TypeSignatureProvider(_signatureBuilder);

		Assert.NotNull(provider);
	}

	[Fact]
	public void GetSignature_ReturnsConsistentResults()
	{
		var expectedSignature = "consistent-signature";
		_signatureBuilder.GetSignature(typeof(TestEntity)).Returns(expectedSignature);

		var signature1 = _provider.GetSignature(typeof(TestEntity));
		var signature2 = _provider.GetSignature(typeof(TestEntity));
		var signature3 = _provider.GetSignature(typeof(TestEntity));

		Assert.Equal(signature1, signature2);
		Assert.Equal(signature2, signature3);
	}
}
