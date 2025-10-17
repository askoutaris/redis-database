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

public class RedisCollectionsFactoryChildEntityCollectionTests
{
	private readonly IConnectionMultiplexer _multiplexer;
	private readonly IPeriodicTriggerFactory _triggerFactory;
	private readonly IPeriodicTrigger _trigger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly RedisCollectionsFactory _factory;
	private readonly IRedisContext _context;
	private readonly IRedisSerializer _serializer;

	public RedisCollectionsFactoryChildEntityCollectionTests()
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
	public void RegisterChildEntityCollection_WithValidConfiguration_RegistersSuccessfully()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		var collection = _factory.GetChildEntityCollection<int, string, TestEntity>(_context);

		Assert.NotNull(collection);
	}

	[Fact]
	public void RegisterChildEntityCollection_WithNullConfiguration_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			_factory.RegisterChildEntityCollection<int, string, TestEntity>(null!));
	}

	[Fact]
	public void RegisterChildEntityCollection_CalledTwiceWithSameTypes_ThrowsInvalidOperationException()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
				steps.WithKeySpace("test2")
					.WithChildKeyPrefix("child2")
					.WithSerializer(_serializer)
					.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
					.WithUniqueParentKeyFactory(id => id.ToString())
					.WithUniqueChildKeyFactory(childKey => childKey)));

		Assert.Contains("already registered", exception.Message);
	}

	[Fact]
	public void GetChildEntityCollection_WithNullContext_ThrowsArgumentNullException()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		Assert.Throws<ArgumentNullException>(() =>
			_factory.GetChildEntityCollection<int, string, TestEntity>(null!));
	}

	[Fact]
	public void GetChildEntityCollection_WithoutRegistration_ThrowsInvalidOperationException()
	{
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetChildEntityCollection<int, string, TestEntity>(_context));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("RegisterChildEntityCollection", exception.Message);
	}

	[Fact]
	public void GetChildEntityCollection_CalledMultipleTimes_ReturnsDifferentInstances()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		var collection1 = _factory.GetChildEntityCollection<int, string, TestEntity>(_context);
		var collection2 = _factory.GetChildEntityCollection<int, string, TestEntity>(_context);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
		Assert.NotSame(collection1, collection2);
	}

	[Fact]
	public void RegisterChildEntityCollection_WithCustomLifetimeProvider_RegistersSuccessfully()
	{
		var lifetimeProvider = Substitute.For<ILifetimeProvider>();

		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithCustomLifetimeProvider(lifetimeProvider)
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		var collection = _factory.GetChildEntityCollection<int, string, TestEntity>(_context);

		Assert.NotNull(collection);
	}

	[Fact]
	public void RegisterChildEntityCollection_WithDifferentTypes_RegistersMultipleCollections()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test1")
				.WithChildKeyPrefix("child1")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		_factory.RegisterChildEntityCollection<string, int, TestEntity>(steps =>
			steps.WithKeySpace("test2")
				.WithChildKeyPrefix("child2")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id)
				.WithUniqueChildKeyFactory(childKey => childKey.ToString()));

		var collection1 = _factory.GetChildEntityCollection<int, string, TestEntity>(_context);
		var collection2 = _factory.GetChildEntityCollection<string, int, TestEntity>(_context);

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
	}

	[Fact]
	public void GetChildEntityCollection_ReturnsChildEntityCollectionType()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey));

		var collection = _factory.GetChildEntityCollection<int, string, TestEntity>(_context);

		Assert.IsType<IChildEntityCollection<int, string, TestEntity>>(collection, exactMatch: false);
	}

	[Fact]
	public void GetChildEntityCollection_WithWrongBuilderTypeInDictionary_ThrowsInvalidOperationException()
	{
		// First, register an EntityCollection (wrong type) then try to get a ChildEntityCollection
		// This simulates internal state corruption where the wrong builder type is stored
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

		// Get the ChildEntityCollection key
		var childKeyMethodInfo = typeof(RedisCollectionsFactory).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
			.First(m => m.Name == "GetBuilderKey" && m.GetGenericArguments().Length == 3);
		var childKeyMethod = childKeyMethodInfo.MakeGenericMethod(typeof(int), typeof(string), typeof(TestEntity));
		var childKey = childKeyMethod.Invoke(null, new object[] { "default" }) as string;
		Assert.NotNull(childKey);

		// Get the EntityCollectionBuilder
		var builderType = builders.GetType();
		var getItemMethod = builderType.GetMethod("get_Item");
		Assert.NotNull(getItemMethod);
		var entityBuilder = getItemMethod.Invoke(builders, new object[] { entityKey });
		Assert.NotNull(entityBuilder);

		// Put the EntityCollectionBuilder in the ChildEntityCollection slot (wrong builder type)
		var setItemMethod = builderType.GetMethod("set_Item");
		Assert.NotNull(setItemMethod);
		setItemMethod.Invoke(builders, new object[] { childKey, entityBuilder });

		// Act & Assert - Now try to get ChildEntityCollection with the wrong builder type
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetChildEntityCollection<int, string, TestEntity>(_context));

		Assert.Contains("Registered builder is of type", exception.Message);
		Assert.Contains("instead of", exception.Message);
	}

	[Fact]
	public void RegisterChildEntityCollection_WithDifferentNames_RegistersMultipleCollections()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test1")
				.WithChildKeyPrefix("child1")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey), "first");

		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test2")
				.WithChildKeyPrefix("child2")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey), "second");

		var collection1 = _factory.GetChildEntityCollection<int, string, TestEntity>(_context, "first");
		var collection2 = _factory.GetChildEntityCollection<int, string, TestEntity>(_context, "second");

		Assert.NotNull(collection1);
		Assert.NotNull(collection2);
	}

	[Fact]
	public void RegisterChildEntityCollection_CalledTwiceWithSameTypesAndSameName_ThrowsInvalidOperationException()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey), "custom");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
				steps.WithKeySpace("test2")
					.WithChildKeyPrefix("child2")
					.WithSerializer(_serializer)
					.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
					.WithUniqueParentKeyFactory(id => id.ToString())
					.WithUniqueChildKeyFactory(childKey => childKey), "custom"));

		Assert.Contains("already registered", exception.Message);
		Assert.Contains("with name 'custom'", exception.Message);
	}

	[Fact]
	public void GetChildEntityCollection_WithUnregisteredName_ThrowsInvalidOperationException()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test")
				.WithChildKeyPrefix("child")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey), "first");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetChildEntityCollection<int, string, TestEntity>(_context, "second"));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("with name 'second'", exception.Message);
	}

	[Fact]
	public void GetChildEntityCollection_WithNamedRegistration_ReturnsCorrectCollection()
	{
		_factory.RegisterChildEntityCollection<int, string, TestEntity>(steps =>
			steps.WithKeySpace("test-custom")
				.WithChildKeyPrefix("child-custom")
				.WithSerializer(_serializer)
				.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
				.WithUniqueParentKeyFactory(id => id.ToString())
				.WithUniqueChildKeyFactory(childKey => childKey), "custom");

		var collection = _factory.GetChildEntityCollection<int, string, TestEntity>(_context, "custom");

		Assert.NotNull(collection);
	}
}
