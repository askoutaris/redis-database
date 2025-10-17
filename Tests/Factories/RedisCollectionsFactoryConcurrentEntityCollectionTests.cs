using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase;
using RedisDatabase.Collections;
using RedisDatabase.Factories;
using RedisDatabase.LifetimeProvider;
using RedisDatabase.PeriodicTriggers;
using RedisDatabase.Serializers;
using StackExchange.Redis;

namespace Tests.Factories;

public class RedisCollectionsFactoryConcurrentEntityCollectionTests
{
	private readonly IConnectionMultiplexer _multiplexer;
	private readonly IPeriodicTriggerFactory _triggerFactory;
	private readonly IPeriodicTrigger _trigger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly RedisCollectionsFactory _factory;
	private readonly IRedisContext _context;
	private readonly IRedisSerializer _serializer;

	public RedisCollectionsFactoryConcurrentEntityCollectionTests()
	{
		_multiplexer = Substitute.For<IConnectionMultiplexer>();
		_triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		_trigger = Substitute.For<IPeriodicTrigger>();
		_loggerFactory = Substitute.For<ILoggerFactory>();
		_context = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();

		_triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(_trigger);
		_factory = new RedisCollectionsFactory(_multiplexer, _triggerFactory, _loggerFactory);
	}

	public class TestEntity
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Version { get; set; } = string.Empty;
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_WithValidConfiguration_RegistersSuccessfully()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		var collection = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context);

		Assert.NotNull(collection);
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_WithNullConfiguration_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			_factory.RegisterConcurrentEntityCollection<int, TestEntity>(null!));
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_CalledTwiceWithSameTypes_ThrowsInvalidOperationException()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
				steps.WithKeySpace("test2")
					.WithSerializer(_serializer)
					.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
					.WithUniqueKeyFactory(id => id.ToString())
					.WithOldConcurrencyTokenSelector(e => e.Version)
					.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString())));

		Assert.Contains("already registered", exception.Message);
	}

	[Fact]
	public void GetConcurrentEntityCollection_WithNullContext_ThrowsArgumentNullException()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		Assert.Throws<ArgumentNullException>(() =>
			_factory.GetConcurrentEntityCollection<int, TestEntity>(null!));
	}

	[Fact]
	public void GetConcurrentEntityCollection_WithoutRegistration_ThrowsInvalidOperationException()
	{
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetConcurrentEntityCollection<int, TestEntity>(_context));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("RegisterConcurrentEntityCollection", exception.Message);
	}

	[Fact]
	public void GetConcurrentEntityCollection_CalledMultipleTimes_ReturnsDifferentInstances()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		var collection1 = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context);
		var collection2 = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
		Assert.NotSame(collection1, collection2);
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_WithCustomLifetimeProvider_RegistersSuccessfully()
	{
		var lifetimeProvider = Substitute.For<ILifetimeProvider>();

		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithCustomLifetimeProvider(lifetimeProvider)
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		var collection = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context);

		Assert.NotNull(collection);
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_WithDifferentTypes_RegistersMultipleCollections()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test1")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		_factory.RegisterConcurrentEntityCollection<string, TestEntity>(steps =>
			steps.WithKeySpace("test2")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id)
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		var collection1 = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context);
		var collection2 = _factory.GetConcurrentEntityCollection<string, TestEntity>(_context);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
	}

	[Fact]
	public void GetConcurrentEntityCollection_ReturnsEntityCollectionType()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()));

		var collection = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context);

		Assert.IsType<IEntityCollection<int, TestEntity>>(collection, exactMatch: false);
	}

	[Fact]
	public void GetConcurrentEntityCollection_WithWrongBuilderTypeInDictionary_ThrowsInvalidOperationException()
	{
		// Register an EntityCollection (wrong type) then try to get a ConcurrentEntityCollection
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		// Use reflection to get the builders dictionary
		var buildersField = typeof(RedisCollectionsFactory).GetField("_builders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		Assert.NotNull(buildersField);

		var builders = buildersField.GetValue(_factory);
		Assert.NotNull(builders);

		// Get the EntityCollection key and builder
		var entityKeyMethodInfo = typeof(RedisCollectionsFactory).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
			.First(m => m.Name == "GetBuilderKey" && m.GetGenericArguments().Length == 2);
		var entityKeyMethod = entityKeyMethodInfo.MakeGenericMethod(typeof(int), typeof(TestEntity));
		var entityKey = entityKeyMethod.Invoke(null, new object[] { "default" }) as string;
		Assert.NotNull(entityKey);

		// Get the EntityCollectionBuilder
		var builderType = builders.GetType();
		var getItemMethod = builderType.GetMethod("get_Item");
		Assert.NotNull(getItemMethod);
		var entityBuilder = getItemMethod.Invoke(builders, new object[] { entityKey });
		Assert.NotNull(entityBuilder);

		// Put the EntityCollectionBuilder in the ConcurrentEntityCollection slot (wrong builder type)
		// Both use the same key, so we're simulating internal corruption
		var setItemMethod = builderType.GetMethod("set_Item");
		Assert.NotNull(setItemMethod);
		setItemMethod.Invoke(builders, new object[] { entityKey, entityBuilder });

		// Act & Assert - Now try to get ConcurrentEntityCollection with the wrong builder type
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetConcurrentEntityCollection<int, TestEntity>(_context));

		Assert.Contains("Registered builder is of type", exception.Message);
		Assert.Contains("instead of", exception.Message);
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_WithDifferentNames_RegistersMultipleCollections()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test1")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()), "first");

		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test2")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()), "second");

		var collection1 = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context, "first");
		var collection2 = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context, "second");

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
	}

	[Fact]
	public void RegisterConcurrentEntityCollection_CalledTwiceWithSameTypesAndSameName_ThrowsInvalidOperationException()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()), "custom");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
				steps.WithKeySpace("test2")
					.WithSerializer(_serializer)
					.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
					.WithUniqueKeyFactory(id => id.ToString())
					.WithOldConcurrencyTokenSelector(e => e.Version)
					.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()), "custom"));

		Assert.Contains("already registered", exception.Message);
		Assert.Contains("with name 'custom'", exception.Message);
	}

	[Fact]
	public void GetConcurrentEntityCollection_WithUnregisteredName_ThrowsInvalidOperationException()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()), "first");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetConcurrentEntityCollection<int, TestEntity>(_context, "second"));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("with name 'second'", exception.Message);
	}

	[Fact]
	public void GetConcurrentEntityCollection_WithNamedRegistration_ReturnsCorrectCollection()
	{
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test-custom")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => e.Version)
				.WithNewConcurrencyTokenSelector(e => Guid.NewGuid().ToString()), "custom");

		var collection = _factory.GetConcurrentEntityCollection<int, TestEntity>(_context, "custom");

		Assert.NotNull(collection);
	}
}
