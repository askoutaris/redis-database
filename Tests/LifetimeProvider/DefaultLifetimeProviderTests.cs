using RedisDatabase.LifetimeProvider;

namespace Tests.LifetimeProvider;

public class DefaultLifetimeProviderTests
{
	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var expiration = TimeSpan.FromMinutes(5);
		var extendOnReads = true;

		var provider = new DefaultLifetimeProvider(expiration, extendOnReads);

		Assert.NotNull(provider);
	}

	[Fact]
	public void GetCachingExpiration_ReturnsConfiguredLifetime()
	{
		// Arrange
		var expiration = TimeSpan.FromMinutes(10);
		var extendOnReads = false;
		var provider = new DefaultLifetimeProvider(expiration, extendOnReads);

		// Act
		var lifetime = provider.GetCachingExpiration(123);

		// Assert
		Assert.Equal(expiration, lifetime.Expiration);
		Assert.Equal(extendOnReads, lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithDifferentKeys_ReturnsSameLifetime()
	{
		// Arrange
		var expiration = TimeSpan.FromMinutes(15);
		var extendOnReads = true;
		var provider = new DefaultLifetimeProvider(expiration, extendOnReads);

		// Act
		var lifetime1 = provider.GetCachingExpiration(1);
		var lifetime2 = provider.GetCachingExpiration(2);
		var lifetime3 = provider.GetCachingExpiration("key");

		// Assert
		Assert.Equal(lifetime1.Expiration, lifetime2.Expiration);
		Assert.Equal(lifetime1.ExtendExpirationOnReads, lifetime2.ExtendExpirationOnReads);
		Assert.Equal(lifetime2.Expiration, lifetime3.Expiration);
		Assert.Equal(lifetime2.ExtendExpirationOnReads, lifetime3.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithExtendOnReadsTrue_ReturnsCorrectValue()
	{
		// Arrange
		var expiration = TimeSpan.FromSeconds(30);
		var provider = new DefaultLifetimeProvider(expiration, extendExpirationOnReads: true);

		// Act
		var lifetime = provider.GetCachingExpiration("test-key");

		// Assert
		Assert.True(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithExtendOnReadsFalse_ReturnsCorrectValue()
	{
		// Arrange
		var expiration = TimeSpan.FromSeconds(45);
		var provider = new DefaultLifetimeProvider(expiration, extendExpirationOnReads: false);

		// Act
		var lifetime = provider.GetCachingExpiration("test-key");

		// Assert
		Assert.False(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void Constructor_WithZeroExpiration_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
			new DefaultLifetimeProvider(TimeSpan.Zero, false));

		Assert.Equal("expiration", ex.ParamName);
	}

	[Fact]
	public void Constructor_WithNegativeExpiration_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
			new DefaultLifetimeProvider(TimeSpan.FromMinutes(-1), false));

		Assert.Equal("expiration", ex.ParamName);
	}

	[Fact]
	public void Constructor_WithNullExpiration_InitializesCorrectly()
	{
		// Arrange & Act
		var provider = new DefaultLifetimeProvider(null, false);

		// Assert
		Assert.NotNull(provider);
	}

	[Fact]
	public void GetCachingExpiration_WithNullExpiration_ReturnsNullExpiration()
	{
		// Arrange
		var provider = new DefaultLifetimeProvider(null, false);

		// Act
		var lifetime = provider.GetCachingExpiration(1);

		// Assert
		Assert.Null(lifetime.Expiration);
		Assert.False(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithNullExpirationAndExtendOnReads_ReturnsCorrectValues()
	{
		// Arrange
		var provider = new DefaultLifetimeProvider(null, true);

		// Act
		var lifetime = provider.GetCachingExpiration(1);

		// Assert
		Assert.Null(lifetime.Expiration);
		Assert.True(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithLargeExpiration_ReturnsLargeExpiration()
	{
		// Arrange
		var expiration = TimeSpan.FromDays(365);
		var provider = new DefaultLifetimeProvider(expiration, true);

		// Act
		var lifetime = provider.GetCachingExpiration(1);

		// Assert
		Assert.Equal(expiration, lifetime.Expiration);
	}

	[Fact]
	public void GetCachingExpiration_CalledMultipleTimes_ReturnsSameLifetimeInstance()
	{
		// Arrange
		var expiration = TimeSpan.FromMinutes(20);
		var provider = new DefaultLifetimeProvider(expiration, true);

		// Act
		var lifetime1 = provider.GetCachingExpiration(1);
		var lifetime2 = provider.GetCachingExpiration(1);
		var lifetime3 = provider.GetCachingExpiration(2);

		// Assert - all should have identical values (same cached instance)
		Assert.Equal(lifetime1.Expiration, lifetime2.Expiration);
		Assert.Equal(lifetime1.ExtendExpirationOnReads, lifetime2.ExtendExpirationOnReads);
		Assert.Equal(lifetime2.Expiration, lifetime3.Expiration);
		Assert.Equal(lifetime2.ExtendExpirationOnReads, lifetime3.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithStringKey_WorksCorrectly()
	{
		// Arrange
		var expiration = TimeSpan.FromMinutes(5);
		var provider = new DefaultLifetimeProvider(expiration, false);

		// Act
		var lifetime = provider.GetCachingExpiration("my-key");

		// Assert
		Assert.Equal(expiration, lifetime.Expiration);
		Assert.False(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithNullKey_WorksCorrectly()
	{
		// Arrange
		var expiration = TimeSpan.FromMinutes(5);
		var provider = new DefaultLifetimeProvider(expiration, false);

		// Act
		var lifetime = provider.GetCachingExpiration<string>(null!);

		// Assert
		Assert.Equal(expiration, lifetime.Expiration);
		Assert.False(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void GetCachingExpiration_WithComplexKey_WorksCorrectly()
	{
		// Arrange
		var expiration = TimeSpan.FromMinutes(5);
		var provider = new DefaultLifetimeProvider(expiration, true);
		var complexKey = new { Id = 1, Name = "Test" };

		// Act
		var lifetime = provider.GetCachingExpiration(complexKey);

		// Assert
		Assert.Equal(expiration, lifetime.Expiration);
		Assert.True(lifetime.ExtendExpirationOnReads);
	}

	[Fact]
	public void ImplementsILifetimeProvider_Interface()
	{
		// Arrange
		var provider = new DefaultLifetimeProvider(TimeSpan.FromMinutes(1), false);

		// Assert
		Assert.IsAssignableFrom<ILifetimeProvider>(provider);
	}
}
