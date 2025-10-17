using RedisDatabase.Serializers.RedisSerializerSignatureDecorators;

namespace RedisDatabaseTests.Serializers.RedisSerializerSignatureDecoratorsTests;

public class CacheEntryTests
{
	[Fact]
	public void Constructor_WithValidParameters_SetsProperties()
	{
		var bytes = new byte[] { 1, 2, 3 };
		var signature = "test-signature";

		var entry = new CacheEntry(bytes, signature);

		Assert.Equal(bytes, entry.Bytes);
		Assert.Equal(signature, entry.Signature);
	}

	[Fact]
	public void Constructor_WithNullBytes_SetsNullBytes()
	{
		var signature = "test-signature";

		var entry = new CacheEntry(null, signature);

		Assert.Null(entry.Bytes);
		Assert.Equal(signature, entry.Signature);
	}

	[Fact]
	public void Constructor_WithEmptyBytes_SetsEmptyBytes()
	{
		var bytes = Array.Empty<byte>();
		var signature = "test-signature";

		var entry = new CacheEntry(bytes, signature);

		Assert.Empty(entry.Bytes!);
		Assert.Equal(signature, entry.Signature);
	}

	[Fact]
	public void Bytes_Property_IsReadOnly()
	{
		var bytes = new byte[] { 1, 2, 3 };
		var signature = "test-signature";
		var entry = new CacheEntry(bytes, signature);

		var retrievedBytes = entry.Bytes;

		Assert.Same(bytes, retrievedBytes);
	}

	[Fact]
	public void Signature_Property_IsReadOnly()
	{
		var bytes = new byte[] { 1, 2, 3 };
		var signature = "test-signature";
		var entry = new CacheEntry(bytes, signature);

		var retrievedSignature = entry.Signature;

		Assert.Same(signature, retrievedSignature);
	}

	[Fact]
	public void CacheEntry_IsSerializable()
	{
		var attributes = typeof(CacheEntry).GetCustomAttributes(typeof(SerializableAttribute), false);

		Assert.NotEmpty(attributes);
	}
}
