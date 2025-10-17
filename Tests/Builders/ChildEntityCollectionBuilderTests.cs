using RedisDatabase;
using RedisDatabase.Builders;
using RedisDatabase.Collections;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using RedisDatabase.Serializers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase.Exceptions;

namespace Tests.Builders;

public class ChildEntityCollectionBuilderTests
{
	private readonly IExpirationUpdater _defaultExpirationUpdater;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IRedisContext _redisContext;
	private readonly IRedisSerializer _serializer;
	private readonly ILifetimeProvider _lifetimeProvider;
	private readonly Func<int, string> _uniqueParentKeyFactory;
	private readonly Func<string, string> _uniqueChildKeyFactory;

	public ChildEntityCollectionBuilderTests()
	{
		_defaultExpirationUpdater = Substitute.For<IExpirationUpdater>();
		_loggerFactory = Substitute.For<ILoggerFactory>();
		_redisContext = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
		_lifetimeProvider = Substitute.For<ILifetimeProvider>();
		_uniqueParentKeyFactory = parentKey => parentKey.ToString();
		_uniqueChildKeyFactory = childKey => childKey;
	}

	private class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidDefaultExpirationUpdater_InitializesCorrectly()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.NotNull(builder);
	}

	#endregion

	#region WithKeySpace Tests

	[Fact]
	public void WithKeySpace_WithValidKeySpace_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithKeySpace("test:parent");

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithKeySpace_WithNullKeySpace_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithKeySpace(null!));
	}

	#endregion

	#region WithChildKeyPrefix Tests

	[Fact]
	public void WithChildKeyPrefix_WithValidPrefix_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithChildKeyPrefix("child");

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithChildKeyPrefix_WithNullPrefix_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithChildKeyPrefix(null!));
	}

	#endregion

	#region WithSerializer Tests

	[Fact]
	public void WithSerializer_WithValidSerializer_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithSerializer(_serializer);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithSerializer(null!));
	}

	#endregion

	#region WithSignaturedSerializer Tests

	[Fact]
	public void WithSignaturedSerializer_WithValidSerializer_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithSignaturedSerializer(_serializer);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithSignaturedSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithSignaturedSerializer(null!));
	}

	[Fact]
	public void WithSignaturedSerializer_BuildsSuccessfully()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSignaturedSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<ChildEntityCollection<int, string, TestEntity>>(collection);
	}

	#endregion

	#region WithCustomLifetimeProvider Tests

	[Fact]
	public void WithCustomLifetimeProvider_WithValidProvider_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithCustomLifetimeProvider(_lifetimeProvider);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithCustomLifetimeProvider_WithNullProvider_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithCustomLifetimeProvider(null!));
	}

	#endregion

	#region WithDefaultLifetimeProvider Tests

	[Fact]
	public void WithDefaultLifetimeProvider_WithValidExpiration_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5));

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithValidExpirationAndExtendOnReads_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5), true);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithZeroExpiration_ThrowsArgumentOutOfRangeException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			builder.WithDefaultLifetimeProvider(TimeSpan.Zero));

		Assert.Equal("expiration", exception.ParamName);
		Assert.Contains("Expiration must be greater than zero", exception.Message);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithNegativeExpiration_ThrowsArgumentOutOfRangeException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			builder.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(-1)));

		Assert.Equal("expiration", exception.ParamName);
		Assert.Contains("Expiration must be greater than zero", exception.Message);
	}

	#endregion

	#region WithNoExpiration Tests

	[Fact]
	public void WithNoExpiration_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithNoExpiration();

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithNoExpiration_CreatesDefaultLifetimeProviderWithNullExpiration()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithNoExpiration()
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
	}

	#endregion

	#region WithExpirationUpdater Tests

	[Fact]
	public void WithExpirationUpdater_WithValidUpdater_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);
		var customUpdater = Substitute.For<IExpirationUpdater>();

		var result = builder.WithExpirationUpdater(customUpdater);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithExpirationUpdater_WithNullUpdater_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithExpirationUpdater(null!));
	}

	#endregion

	#region WithUniqueParentKeyFactory Tests

	[Fact]
	public void WithUniqueParentKeyFactory_WithValidFactory_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithUniqueParentKeyFactory(_uniqueParentKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithUniqueParentKeyFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithUniqueParentKeyFactory(null!));
	}

	#endregion

	#region WithUniqueChildKeyFactory Tests

	[Fact]
	public void WithUniqueChildKeyFactory_WithValidFactory_ReturnsBuilder()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithUniqueChildKeyFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithUniqueChildKeyFactory(null!));
	}

	#endregion

	#region Build Tests

	[Fact]
	public void Build_WithAllParametersConfigured_ReturnsChildEntityCollection()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<ChildEntityCollection<int, string, TestEntity>>(collection);
	}

	[Fact]
	public void Build_WithDefaultLifetimeProvider_ReturnsChildEntityCollection()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<ChildEntityCollection<int, string, TestEntity>>(collection);
	}

	[Fact]
	public void Build_WithCustomExpirationUpdater_ReturnsChildEntityCollection()
	{
		var customUpdater = Substitute.For<IExpirationUpdater>();
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithExpirationUpdater(customUpdater)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<ChildEntityCollection<int, string, TestEntity>>(collection);
	}

	[Fact]
	public void Build_WithNullContext_ThrowsArgumentNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		Assert.Throws<ArgumentNullException>(() => builder.Build(null!));
	}

	[Fact]
	public void Build_WithoutKeySpace_ThrowsParameterNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("KeySpace cannot be null", exception.Message);
		Assert.Contains("WithKeySpace", exception.Message);
	}

	[Fact]
	public void Build_WithoutChildKeyPrefix_ThrowsParameterNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("ChildKeyPrefix cannot be null", exception.Message);
		Assert.Contains("WithChildKeyPrefix", exception.Message);
	}

	[Fact]
	public void Build_WithoutSerializer_ThrowsParameterNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("Serializer cannot be null", exception.Message);
		Assert.Contains("WithSerializer", exception.Message);
	}

	[Fact]
	public void Build_WithoutLifetimeProvider_ThrowsParameterNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("LifetimeProvider cannot be null", exception.Message);
		Assert.Contains("WithCustomLifetimeProvider", exception.Message);
	}

	[Fact]
	public void Build_WithoutUniqueParentKeyFactory_ThrowsParameterNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("UniqueParentKeyFactory cannot be null", exception.Message);
		Assert.Contains("WithUniqueParentKeyFactory", exception.Message);
	}

	[Fact]
	public void Build_WithoutUniqueChildKeyFactory_ThrowsParameterNullException()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("UniqueChildKeyFactory cannot be null", exception.Message);
		Assert.Contains("WithUniqueChildKeyFactory", exception.Message);
	}

	[Fact]
	public void Build_CalledMultipleTimes_ReturnsNewInstanceEachTime()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		var collection1 = builder.Build(_redisContext);
		var collection2 = builder.Build(_redisContext);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
		Assert.NotSame(collection1, collection2);
	}

	#endregion

	#region Fluent API Tests

	[Fact]
	public void FluentConfiguration_CanChainAllMethods()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithExpirationUpdater(_defaultExpirationUpdater)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void FluentConfiguration_WithDefaultLifetimeProvider_CanChainAllMethods()
	{
		var builder = new ChildEntityCollectionBuilder<int, string, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder
			.WithKeySpace("test:parent")
			.WithChildKeyPrefix("child")
			.WithSerializer(_serializer)
			.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5), true)
			.WithExpirationUpdater(_defaultExpirationUpdater)
			.WithUniqueParentKeyFactory(_uniqueParentKeyFactory)
			.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		Assert.Same(builder, result);
	}

	#endregion
}
