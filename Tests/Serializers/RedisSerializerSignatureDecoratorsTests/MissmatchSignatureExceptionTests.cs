using RedisDatabase.Serializers.RedisSerializerSignatureDecorators;

namespace RedisDatabaseTests.Serializers.RedisSerializerSignatureDecoratorsTests;

public class MismatchSignatureExceptionTests
{
	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		var message = "Test error message";

		var exception = new MismatchSignatureException(message);

		Assert.Equal(message, exception.Message);
	}

	[Fact]
	public void Constructor_WithNullMessage_SetsNullMessage()
	{
		var exception = new MismatchSignatureException(null);

		Assert.NotNull(exception.Message);
	}

	[Fact]
	public void Exception_InheritsFromException()
	{
		var exception = new MismatchSignatureException("test");

		Assert.IsType<Exception>(exception, exactMatch: false);
	}

	[Fact]
	public void Exception_CanBeCaught()
	{
		try
		{
			throw new MismatchSignatureException("test error");
		}
		catch (MismatchSignatureException ex)
		{
			Assert.Equal("test error", ex.Message);
		}
	}
}
