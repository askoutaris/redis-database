using RedisDatabase.Serializers;
using RedisDatabase.Serializers.RedisSerializerSignatureDecorators;
using NSubstitute;

namespace RedisDatabaseTests.Serializers.RedisSerializerSignatureDecoratorsTests;

public class RedisSerializerSignatureDecoratorTests
{
	private readonly IRedisSerializer _innerSerializer;
	private readonly ITypeSignatureProvider _signatureProvider;
	private readonly RedisSerializerSignatureDecorator _decorator;

	public RedisSerializerSignatureDecoratorTests()
	{
		_innerSerializer = Substitute.For<IRedisSerializer>();
		_signatureProvider = Substitute.For<ITypeSignatureProvider>();
		_decorator = new RedisSerializerSignatureDecorator(_innerSerializer, _signatureProvider);
	}

	public class TestEntity
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	[Fact]
	public void Serialize_WithValidObject_CreatesCacheEntryWithSignature()
	{
		var entity = new TestEntity { Id = 1, Name = "Test" };
		var expectedBytes = new byte[] { 1, 2, 3 };
		var expectedSignature = "test-signature";
		var expectedCacheEntryBytes = new byte[] { 4, 5, 6 };

		_innerSerializer.Serialize(entity).Returns(expectedBytes);
		_signatureProvider.GetSignature(typeof(TestEntity)).Returns(expectedSignature);
		_innerSerializer.Serialize(Arg.Any<CacheEntry>()).Returns(expectedCacheEntryBytes);

		var result = _decorator.Serialize(entity);

		Assert.Equal(expectedCacheEntryBytes, result);
		_innerSerializer.Received(1).Serialize(entity);
		_signatureProvider.Received(1).GetSignature(typeof(TestEntity));
		_innerSerializer.Received(1).Serialize(Arg.Is<CacheEntry>(e =>
			e.Bytes == expectedBytes && e.Signature == expectedSignature));
	}

	[Fact]
	public void Deserialize_WithValidCacheEntry_ReturnsObject()
	{
		var expectedEntity = new TestEntity { Id = 1, Name = "Test" };
		var entityBytes = new byte[] { 1, 2, 3 };
		var signature = "test-signature";
		var cacheEntry = new CacheEntry(entityBytes, signature);
		var cacheEntryBytes = new byte[] { 4, 5, 6 };

		_innerSerializer.Deserialize<CacheEntry>(cacheEntryBytes).Returns(cacheEntry);
		_signatureProvider.GetSignature(typeof(TestEntity)).Returns(signature);
		_innerSerializer.Deserialize<TestEntity>(entityBytes).Returns(expectedEntity);

		var result = _decorator.Deserialize<TestEntity>(cacheEntryBytes);

		Assert.NotNull(result);
		Assert.Equal(expectedEntity.Id, result.Id);
		Assert.Equal(expectedEntity.Name, result.Name);
		_innerSerializer.Received(1).Deserialize<CacheEntry>(cacheEntryBytes);
		_signatureProvider.Received(1).GetSignature(typeof(TestEntity));
		_innerSerializer.Received(1).Deserialize<TestEntity>(entityBytes);
	}

	[Fact]
	public void Deserialize_WithNullCacheEntry_ReturnsDefault()
	{
		var cacheEntryBytes = new byte[] { 1, 2, 3 };
		_innerSerializer.Deserialize<CacheEntry>(cacheEntryBytes).Returns((CacheEntry?)null);

		var result = _decorator.Deserialize<TestEntity>(cacheEntryBytes);

		Assert.Null(result);
		_signatureProvider.DidNotReceive().GetSignature(Arg.Any<Type>());
	}

	[Fact]
	public void Deserialize_WithNullBytes_ReturnsDefault()
	{
		var cacheEntry = new CacheEntry(null, "signature");
		var cacheEntryBytes = new byte[] { 1, 2, 3 };
		_innerSerializer.Deserialize<CacheEntry>(cacheEntryBytes).Returns(cacheEntry);

		var result = _decorator.Deserialize<TestEntity>(cacheEntryBytes);

		Assert.Null(result);
		_signatureProvider.DidNotReceive().GetSignature(Arg.Any<Type>());
	}

	[Fact]
	public void Deserialize_WithMismatchedSignature_ThrowsMismatchSignatureException()
	{
		var entityBytes = new byte[] { 1, 2, 3 };
		var cacheEntry = new CacheEntry(entityBytes, "old-signature");
		var cacheEntryBytes = new byte[] { 4, 5, 6 };

		_innerSerializer.Deserialize<CacheEntry>(cacheEntryBytes).Returns(cacheEntry);
		_signatureProvider.GetSignature(typeof(TestEntity)).Returns("new-signature");

		var exception = Assert.Throws<MismatchSignatureException>(() =>
			_decorator.Deserialize<TestEntity>(cacheEntryBytes));

		Assert.Contains(typeof(TestEntity).FullName!, exception.Message);
		_innerSerializer.DidNotReceive().Deserialize<TestEntity>(Arg.Any<byte[]>());
	}

	[Fact]
	public void Deserialize_WithMatchingSignature_DeserializesSuccessfully()
	{
		var expectedEntity = new TestEntity { Id = 42, Name = "Success" };
		var entityBytes = new byte[] { 1, 2, 3 };
		var signature = "matching-signature";
		var cacheEntry = new CacheEntry(entityBytes, signature);
		var cacheEntryBytes = new byte[] { 4, 5, 6 };

		_innerSerializer.Deserialize<CacheEntry>(cacheEntryBytes).Returns(cacheEntry);
		_signatureProvider.GetSignature(typeof(TestEntity)).Returns(signature);
		_innerSerializer.Deserialize<TestEntity>(entityBytes).Returns(expectedEntity);

		var result = _decorator.Deserialize<TestEntity>(cacheEntryBytes);

		Assert.NotNull(result);
		Assert.Equal(42, result.Id);
		Assert.Equal("Success", result.Name);
	}

	[Fact]
	public void Serialize_CallsInnerSerializerTwice()
	{
		var entity = new TestEntity { Id = 1, Name = "Test" };
		var entityBytes = new byte[] { 1, 2, 3 };
		var cacheEntryBytes = new byte[] { 4, 5, 6 };

		_innerSerializer.Serialize(entity).Returns(entityBytes);
		_innerSerializer.Serialize(Arg.Any<CacheEntry>()).Returns(cacheEntryBytes);
		_signatureProvider.GetSignature(typeof(TestEntity)).Returns("sig");

		_decorator.Serialize(entity);

		_innerSerializer.Received(1).Serialize(entity);
		_innerSerializer.Received(1).Serialize(Arg.Any<CacheEntry>());
	}

	[Fact]
	public void Deserialize_WithValidData_CallsInnerSerializerTwice()
	{
		var entityBytes = new byte[] { 1, 2, 3 };
		var cacheEntry = new CacheEntry(entityBytes, "sig");
		var cacheEntryBytes = new byte[] { 4, 5, 6 };

		_innerSerializer.Deserialize<CacheEntry>(cacheEntryBytes).Returns(cacheEntry);
		_signatureProvider.GetSignature(typeof(TestEntity)).Returns("sig");
		_innerSerializer.Deserialize<TestEntity>(entityBytes).Returns(new TestEntity());

		_decorator.Deserialize<TestEntity>(cacheEntryBytes);

		_innerSerializer.Received(1).Deserialize<CacheEntry>(cacheEntryBytes);
		_innerSerializer.Received(1).Deserialize<TestEntity>(entityBytes);
	}

	[Fact]
	public void Constructor_CreatesInstance()
	{
		var decorator = new RedisSerializerSignatureDecorator(_innerSerializer, _signatureProvider);

		Assert.NotNull(decorator);
	}
}
