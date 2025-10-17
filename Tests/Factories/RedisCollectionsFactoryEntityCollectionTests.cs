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

public class RedisCollectionsFactoryEntityCollectionTests
{
	private readonly IConnectionMultiplexer _multiplexer;
	private readonly IPeriodicTriggerFactory _triggerFactory;
	private readonly IPeriodicTrigger _trigger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly RedisCollectionsFactory _factory;
	private readonly IRedisContext _context;
	private readonly IRedisSerializer _serializer;

	public RedisCollectionsFactoryEntityCollectionTests()
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
	}

	[Fact]
	public void RegisterEntityCollection_WithValidConfiguration_RegistersSuccessfully()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		var collection = _factory.GetEntityCollection<int, TestEntity>(_context);

		Assert.NotNull(collection);
	}

	[Fact]
	public void RegisterEntityCollection_WithNullConfiguration_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			_factory.RegisterEntityCollection<int, TestEntity>(null!));
	}

	[Fact]
	public void RegisterEntityCollection_CalledTwiceWithSameTypes_ThrowsInvalidOperationException()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterEntityCollection<int, TestEntity>(steps =>
				steps.WithKeySpace("test2")
					.WithSerializer(_serializer)
					.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
					.WithUniqueKeyFactory(id => id.ToString())));

		Assert.Contains("already registered", exception.Message);
	}

	[Fact]
	public void GetEntityCollection_WithNullContext_ThrowsArgumentNullException()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		Assert.Throws<ArgumentNullException>(() =>
			_factory.GetEntityCollection<int, TestEntity>(null!));
	}

	[Fact]
	public void GetEntityCollection_WithoutRegistration_ThrowsInvalidOperationException()
	{
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetEntityCollection<int, TestEntity>(_context));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("RegisterEntityCollection", exception.Message);
	}

	[Fact]
	public void GetEntityCollection_CalledMultipleTimes_ReturnsDifferentInstances()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		var collection1 = _factory.GetEntityCollection<int, TestEntity>(_context);
		var collection2 = _factory.GetEntityCollection<int, TestEntity>(_context);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
		Assert.NotSame(collection1, collection2);
	}

	[Fact]
	public void RegisterEntityCollection_WithCustomLifetimeProvider_RegistersSuccessfully()
	{
		var lifetimeProvider = Substitute.For<ILifetimeProvider>();

		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithCustomLifetimeProvider(lifetimeProvider)
				.WithUniqueKeyFactory(id => id.ToString()));

		var collection = _factory.GetEntityCollection<int, TestEntity>(_context);

		Assert.NotNull(collection);
	}

	[Fact]
	public void RegisterEntityCollection_WithDifferentTypes_RegistersMultipleCollections()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test1")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		_factory.RegisterEntityCollection<string, TestEntity>(steps =>
			steps.WithKeySpace("test2")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id));

		var collection1 = _factory.GetEntityCollection<int, TestEntity>(_context);
		var collection2 = _factory.GetEntityCollection<string, TestEntity>(_context);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
	}

	[Fact]
	public void GetEntityCollection_ReturnsEntityCollectionType()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()));

		var collection = _factory.GetEntityCollection<int, TestEntity>(_context);

		Assert.IsType<IEntityCollection<int, TestEntity>>(collection, exactMatch: false);
	}

	[Fact]
	public void GetEntityCollection_WithWrongBuilderTypeInDictionary_ThrowsInvalidOperationException()
	{
		// Register a ConcurrentEntityCollection (wrong type) then try to get an EntityCollection
		_factory.RegisterConcurrentEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString())
				.WithOldConcurrencyTokenSelector(e => "old")
				.WithNewConcurrencyTokenSelector(e => "new"));

		// Use reflection to get the builders dictionary
		var buildersField = typeof(RedisCollectionsFactory).GetField("_builders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		Assert.NotNull(buildersField);

		var builders = buildersField.GetValue(_factory);
		Assert.NotNull(builders);

		// Get the ConcurrentEntityCollection key and builder
		var concurrentKeyMethodInfo = typeof(RedisCollectionsFactory).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
			.First(m => m.Name == "GetBuilderKey" && m.GetGenericArguments().Length == 2);
		var concurrentKeyMethod = concurrentKeyMethodInfo.MakeGenericMethod(typeof(int), typeof(TestEntity));
		var concurrentKey = concurrentKeyMethod.Invoke(null, new object[] { "default" }) as string;
		Assert.NotNull(concurrentKey);

		// Get the ConcurrentEntityCollectionBuilder
		var builderType = builders.GetType();
		var getItemMethod = builderType.GetMethod("get_Item");
		Assert.NotNull(getItemMethod);
		var concurrentBuilder = getItemMethod.Invoke(builders, new object[] { concurrentKey });
		Assert.NotNull(concurrentBuilder);

		// Put the ConcurrentEntityCollectionBuilder in the EntityCollection slot (wrong builder type)
		// Both use the same key, so we're simulating internal corruption
		var setItemMethod = builderType.GetMethod("set_Item");
		Assert.NotNull(setItemMethod);
		setItemMethod.Invoke(builders, new object[] { concurrentKey, concurrentBuilder });

		// Act & Assert - Now try to get EntityCollection with the wrong builder type
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetEntityCollection<int, TestEntity>(_context));

		Assert.Contains("Registered builder is of type", exception.Message);
		Assert.Contains("instead of", exception.Message);
	}

	[Fact]
	public void RegisterEntityCollection_WithDifferentNames_RegistersMultipleCollections()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test1")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()), "first");

		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test2")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()), "second");

		var collection1 = _factory.GetEntityCollection<int, TestEntity>(_context, "first");
		var collection2 = _factory.GetEntityCollection<int, TestEntity>(_context, "second");

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
	}

	[Fact]
	public void RegisterEntityCollection_CalledTwiceWithSameTypesAndSameName_ThrowsInvalidOperationException()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()), "custom");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterEntityCollection<int, TestEntity>(steps =>
				steps.WithKeySpace("test2")
					.WithSerializer(_serializer)
					.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
					.WithUniqueKeyFactory(id => id.ToString()), "custom"));

		Assert.Contains("already registered", exception.Message);
		Assert.Contains("with name 'custom'", exception.Message);
	}

	[Fact]
	public void GetEntityCollection_WithUnregisteredName_ThrowsInvalidOperationException()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()), "first");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetEntityCollection<int, TestEntity>(_context, "second"));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("with name 'second'", exception.Message);
	}

	[Fact]
	public void GetEntityCollection_WithNamedRegistration_ReturnsCorrectCollection()
	{
		_factory.RegisterEntityCollection<int, TestEntity>(steps =>
			steps.WithKeySpace("test-custom")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueKeyFactory(id => id.ToString()), "custom");

		var collection = _factory.GetEntityCollection<int, TestEntity>(_context, "custom");

		Assert.NotNull(collection);
	}
}
