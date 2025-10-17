using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Factories;
using RedisDatabase.PeriodicTriggers;
using RedisDatabase.Serializers;
using StackExchange.Redis;

namespace Tests.Factories;

public class RedisCollectionsFactoryStreamAdapterTests
{
	private readonly IConnectionMultiplexer _multiplexer;
	private readonly IPeriodicTriggerFactory _triggerFactory;
	private readonly IPeriodicTrigger _trigger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly RedisCollectionsFactory _factory;
	private readonly IRedisContext _context;
	private readonly IRedisSerializer _serializer;

	public RedisCollectionsFactoryStreamAdapterTests()
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

	public class TestEvent
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	[Fact]
	public void RegisterStreamAdapter_WithValidConfiguration_RegistersSuccessfully()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000));

		var adapter = _factory.GetStreamAdapter<TestEvent>(_context);

		Assert.NotNull(adapter);
	}

	[Fact]
	public void RegisterStreamAdapter_WithNullConfiguration_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() =>
			_factory.RegisterStreamAdapter<TestEvent>(null!));
	}

	[Fact]
	public void RegisterStreamAdapter_CalledTwiceWithSameType_ThrowsInvalidOperationException()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000));

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterStreamAdapter<TestEvent>(steps =>
				steps.WithStreamKey("test-stream-2")
					.WithSerializer(_serializer)
					.WithMaxLength(2000)));

		Assert.Contains("already registered", exception.Message);
	}

	[Fact]
	public void GetStreamAdapter_WithNullContext_ThrowsArgumentNullException()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000));

		Assert.Throws<ArgumentNullException>(() =>
			_factory.GetStreamAdapter<TestEvent>(null!));
	}

	[Fact]
	public void GetStreamAdapter_WithoutRegistration_ThrowsInvalidOperationException()
	{
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetStreamAdapter<TestEvent>(_context));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("RegisterStreamAdapter", exception.Message);
	}

	[Fact]
	public void GetStreamAdapter_CalledMultipleTimes_ReturnsDifferentInstances()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000));

		var adapter1 = _factory.GetStreamAdapter<TestEvent>(_context);
		var adapter2 = _factory.GetStreamAdapter<TestEvent>(_context);

		Assert.NotNull(adapter1);
		Assert.NotNull(adapter2);
		Assert.NotSame(adapter1, adapter2);
	}

	[Fact]
	public void RegisterStreamAdapter_WithDifferentTypes_RegistersMultipleAdapters()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream-1")
				.WithSerializer(_serializer)
				.WithMaxLength(1000));

		_factory.RegisterStreamAdapter<string>(steps =>
			steps.WithStreamKey("test-stream-2")
				.WithSerializer(_serializer)
				.WithMaxLength(2000));

		var adapter1 = _factory.GetStreamAdapter<TestEvent>(_context);
		var adapter2 = _factory.GetStreamAdapter<string>(_context);

		Assert.NotNull(adapter1);
		Assert.NotNull(adapter2);
	}

	[Fact]
	public void GetStreamAdapter_ReturnsStreamAdapterType()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000));

		var adapter = _factory.GetStreamAdapter<TestEvent>(_context);

		Assert.IsType<IStreamAdapter<TestEvent>>(adapter, exactMatch: false);
	}

	[Fact]
	public void GetStreamAdapter_WithWrongBuilderTypeInDictionary_ThrowsInvalidOperationException()
	{
		// Register an EntityCollection (wrong type) then try to get a StreamAdapter
		_factory.RegisterEntityCollection<int, TestEvent>(steps =>
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
		var entityKeyMethod = entityKeyMethodInfo.MakeGenericMethod(typeof(int), typeof(TestEvent));
		var entityKey = entityKeyMethod.Invoke(null, new object[] { "default" }) as string;
		Assert.NotNull(entityKey);

		// Get the StreamAdapter key (single type parameter)
		var streamKeyMethodInfo = typeof(RedisCollectionsFactory).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
			.First(m => m.Name == "GetBuilderKey" && m.GetGenericArguments().Length == 1);
		var streamKeyMethod = streamKeyMethodInfo.MakeGenericMethod(typeof(TestEvent));
		var streamKey = streamKeyMethod.Invoke(null, new object[] { "default" }) as string;
		Assert.NotNull(streamKey);

		// Get the EntityCollectionBuilder
		var builderType = builders.GetType();
		var getItemMethod = builderType.GetMethod("get_Item");
		Assert.NotNull(getItemMethod);
		var entityBuilder = getItemMethod.Invoke(builders, new object[] { entityKey });
		Assert.NotNull(entityBuilder);

		// Put the EntityCollectionBuilder in the StreamAdapter slot (wrong builder type)
		var setItemMethod = builderType.GetMethod("set_Item");
		Assert.NotNull(setItemMethod);
		setItemMethod.Invoke(builders, new object[] { streamKey, entityBuilder });

		// Act & Assert - Now try to get StreamAdapter with the wrong builder type
		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetStreamAdapter<TestEvent>(_context));

		Assert.Contains("Registered stream adapter is of type", exception.Message);
		Assert.Contains("instead of", exception.Message);
	}

	[Fact]
	public void RegisterStreamAdapter_WithDifferentNames_RegistersMultipleAdapters()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream-1")
				.WithSerializer(_serializer)
				.WithMaxLength(1000), "first");

		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream-2")
				.WithSerializer(_serializer)
				.WithMaxLength(2000), "second");

		var adapter1 = _factory.GetStreamAdapter<TestEvent>(_context, "first");
		var adapter2 = _factory.GetStreamAdapter<TestEvent>(_context, "second");

		Assert.NotNull(adapter1);
		Assert.NotNull(adapter2);
	}

	[Fact]
	public void RegisterStreamAdapter_CalledTwiceWithSameTypeAndSameName_ThrowsInvalidOperationException()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000), "custom");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.RegisterStreamAdapter<TestEvent>(steps =>
				steps.WithStreamKey("test-stream-2")
					.WithSerializer(_serializer)
					.WithMaxLength(2000), "custom"));

		Assert.Contains("already registered", exception.Message);
		Assert.Contains("with name 'custom'", exception.Message);
	}

	[Fact]
	public void GetStreamAdapter_WithUnregisteredName_ThrowsInvalidOperationException()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream")
				.WithSerializer(_serializer)
				.WithMaxLength(1000), "first");

		var exception = Assert.Throws<InvalidOperationException>(() =>
			_factory.GetStreamAdapter<TestEvent>(_context, "second"));

		Assert.Contains("not registered", exception.Message);
		Assert.Contains("with name 'second'", exception.Message);
	}

	[Fact]
	public void GetStreamAdapter_WithNamedRegistration_ReturnsCorrectAdapter()
	{
		_factory.RegisterStreamAdapter<TestEvent>(steps =>
			steps.WithStreamKey("test-stream-custom")
				.WithSerializer(_serializer)
				.WithMaxLength(1000), "custom");

		var adapter = _factory.GetStreamAdapter<TestEvent>(_context, "custom");

		Assert.NotNull(adapter);
	}
}
