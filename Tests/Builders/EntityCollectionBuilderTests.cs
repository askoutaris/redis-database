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

public class EntityCollectionBuilderTests
{
	private readonly IExpirationUpdater _defaultExpirationUpdater;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IRedisContext _redisContext;
	private readonly IRedisSerializer _serializer;
	private readonly ILifetimeProvider _lifetimeProvider;
	private readonly Func<int, string> _uniqueKeyFactory;

	public EntityCollectionBuilderTests()
	{
		_defaultExpirationUpdater = Substitute.For<IExpirationUpdater>();
		_loggerFactory = Substitute.For<ILoggerFactory>();
		_redisContext = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
		_lifetimeProvider = Substitute.For<ILifetimeProvider>();
		_uniqueKeyFactory = key => key.ToString();
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
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.NotNull(builder);
	}

	#endregion

	#region WithKeySpace Tests

	[Fact]
	public void WithKeySpace_WithValidKeySpace_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithKeySpace("test:entities");

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithKeySpace_WithNullKeySpace_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithKeySpace(null!));
	}

	#endregion

	#region WithSerializer Tests

	[Fact]
	public void WithSerializer_WithValidSerializer_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithSerializer(_serializer);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithSerializer(null!));
	}

	#endregion

	#region WithSignaturedSerializer Tests

	[Fact]
	public void WithSignaturedSerializer_WithValidSerializer_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithSignaturedSerializer(_serializer);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithSignaturedSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithSignaturedSerializer(null!));
	}

	[Fact]
	public void WithSignaturedSerializer_BuildsSuccessfully()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSignaturedSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<EntityCollection<int, TestEntity>>(collection);
	}

	#endregion

	#region WithCustomLifetimeProvider Tests

	[Fact]
	public void WithCustomLifetimeProvider_WithValidProvider_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithCustomLifetimeProvider(_lifetimeProvider);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithCustomLifetimeProvider_WithNullProvider_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithCustomLifetimeProvider(null!));
	}

	#endregion

	#region WithDefaultLifetimeProvider Tests

	[Fact]
	public void WithDefaultLifetimeProvider_WithValidExpiration_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5));

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithValidExpirationAndExtendOnReads_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5), true);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithZeroExpiration_ThrowsArgumentOutOfRangeException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			builder.WithDefaultLifetimeProvider(TimeSpan.Zero));

		Assert.Equal("expiration", exception.ParamName);
		Assert.Contains("Expiration must be greater than zero", exception.Message);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithNegativeExpiration_ThrowsArgumentOutOfRangeException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

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
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithNoExpiration();

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithNoExpiration_CreatesDefaultLifetimeProviderWithNullExpiration()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithNoExpiration()
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
	}

	#endregion

	#region WithExpirationUpdater Tests

	[Fact]
	public void WithExpirationUpdater_WithValidUpdater_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);
		var customUpdater = Substitute.For<IExpirationUpdater>();

		var result = builder.WithExpirationUpdater(customUpdater);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithExpirationUpdater_WithNullUpdater_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithExpirationUpdater(null!));
	}

	#endregion

	#region WithUniqueKeyFactory Tests

	[Fact]
	public void WithUniqueKeyFactory_WithValidFactory_ReturnsBuilder()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder.WithUniqueKeyFactory(_uniqueKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void WithUniqueKeyFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		Assert.Throws<ArgumentNullException>(() => builder.WithUniqueKeyFactory(null!));
	}

	#endregion

	#region Build Tests

	[Fact]
	public void Build_WithAllParametersConfigured_ReturnsEntityCollection()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<EntityCollection<int, TestEntity>>(collection);
	}

	[Fact]
	public void Build_WithDefaultLifetimeProvider_ReturnsEntityCollection()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<EntityCollection<int, TestEntity>>(collection);
	}

	[Fact]
	public void Build_WithCustomExpirationUpdater_ReturnsEntityCollection()
	{
		var customUpdater = Substitute.For<IExpirationUpdater>();
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithExpirationUpdater(customUpdater)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<EntityCollection<int, TestEntity>>(collection);
	}

	[Fact]
	public void Build_WithNullContext_ThrowsArgumentNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		Assert.Throws<ArgumentNullException>(() => builder.Build(null!));
	}

	[Fact]
	public void Build_WithoutKeySpace_ThrowsParameterNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("KeySpace cannot be null", exception.Message);
		Assert.Contains("WithKeySpace", exception.Message);
	}

	[Fact]
	public void Build_WithoutSerializer_ThrowsParameterNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("Serializer cannot be null", exception.Message);
		Assert.Contains("WithSerializer", exception.Message);
	}

	[Fact]
	public void Build_WithoutLifetimeProvider_ThrowsParameterNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("LifetimeProvider cannot be null", exception.Message);
		Assert.Contains("WithCustomLifetimeProvider", exception.Message);
	}

	[Fact]
	public void Build_WithoutUniqueKeyFactory_ThrowsParameterNullException()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider);

		var exception = Assert.Throws<ParameterNullException>(() => builder.Build(_redisContext));

		Assert.Contains("UniqueKeyFactory cannot be null", exception.Message);
		Assert.Contains("WithUniqueKeyFactory", exception.Message);
	}

	[Fact]
	public void Build_CalledMultipleTimes_ReturnsNewInstanceEachTime()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

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
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithCustomLifetimeProvider(_lifetimeProvider)
			.WithExpirationUpdater(_defaultExpirationUpdater)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void FluentConfiguration_WithDefaultLifetimeProvider_CanChainAllMethods()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5), true)
			.WithExpirationUpdater(_defaultExpirationUpdater)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void FluentConfiguration_WithNoExpiration_CanChainAllMethods()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory);

		var result = builder
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithNoExpiration()
			.WithExpirationUpdater(_defaultExpirationUpdater)
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		Assert.Same(builder, result);
	}

	[Fact]
	public void Build_WithNoExpiration_ReturnsEntityCollection()
	{
		var builder = new EntityCollectionBuilder<int, TestEntity>(_defaultExpirationUpdater, _loggerFactory)
			.WithKeySpace("test:entities")
			.WithSerializer(_serializer)
			.WithNoExpiration()
			.WithUniqueKeyFactory(_uniqueKeyFactory);

		var collection = builder.Build(_redisContext);

		Assert.NotNull(collection);
		Assert.IsType<EntityCollection<int, TestEntity>>(collection);
	}

	#endregion
}
