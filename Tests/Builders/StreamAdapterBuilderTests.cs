using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Builders;
using RedisDatabase.Serializers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Tests.Builders;

public class StreamAdapterBuilderTests
{
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<StreamAdapter<TestMessage>> _logger;
	private readonly StreamAdapterBuilder<TestMessage> _builder;
	private readonly IRedisContext _context;
	private readonly IRedisSerializer _serializer;

	public StreamAdapterBuilderTests()
	{
		_loggerFactory = Substitute.For<ILoggerFactory>();
		_logger = Substitute.For<ILogger<StreamAdapter<TestMessage>>>();
		_loggerFactory.CreateLogger<StreamAdapter<TestMessage>>().Returns(_logger);
		_builder = new StreamAdapterBuilder<TestMessage>(_loggerFactory);
		_context = Substitute.For<IRedisContext>();
		_serializer = Substitute.For<IRedisSerializer>();
	}

	public class TestMessage
	{
		public int Id { get; set; }
		public string Content { get; set; } = string.Empty;
	}

	[Fact]
	public void Constructor_WithValidLoggerFactory_CreatesInstance()
	{
		var loggerFactory = Substitute.For<ILoggerFactory>();
		var logger = Substitute.For<ILogger<StreamAdapter<TestMessage>>>();
		loggerFactory.CreateLogger<StreamAdapter<TestMessage>>().Returns(logger);

		var builder = new StreamAdapterBuilder<TestMessage>(loggerFactory);

		Assert.NotNull(builder);
		loggerFactory.Received(1).CreateLogger<StreamAdapter<TestMessage>>();
	}

	[Fact]
	public void WithStreamKey_WithValidKey_ReturnsBuilder()
	{
		var result = _builder.WithStreamKey("test:stream");

		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithStreamKey_WithNullKey_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentNullException>(() => _builder.WithStreamKey(null!));
	}

	[Fact]
	public void WithStreamKey_WithEmptyKey_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() => _builder.WithStreamKey(string.Empty));
	}

	[Fact]
	public void WithStreamKey_WithWhitespaceKey_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() => _builder.WithStreamKey("   "));
	}

	[Fact]
	public void WithSerializer_WithValidSerializer_ReturnsBuilder()
	{
		var result = _builder.WithSerializer(_serializer);

		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => _builder.WithSerializer(null!));
	}

	[Fact]
	public void WithMaxLength_WithValidValue_ReturnsBuilder()
	{
		var result = _builder.WithMaxLength(1000);

		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithMaxLength_WithZero_ThrowsArgumentOutOfRangeException()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _builder.WithMaxLength(0));

		Assert.Equal("maxLength", exception.ParamName);
		Assert.Contains("MaxLength must be greater than 0", exception.Message);
	}

	[Fact]
	public void WithMaxLength_WithNegativeValue_ThrowsArgumentOutOfRangeException()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _builder.WithMaxLength(-1));

		Assert.Equal("maxLength", exception.ParamName);
	}

	[Fact]
	public void Build_WithAllRequiredParameters_CreatesStreamAdapter()
	{
		_builder.WithStreamKey("test:stream")
			.WithSerializer(_serializer)
			.WithMaxLength(1000);

		var adapter = ((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context);

		Assert.NotNull(adapter);
	}

	[Fact]
	public void Build_WithoutStreamKey_ThrowsInvalidOperationException()
	{
		_builder.WithSerializer(_serializer)
			.WithMaxLength(1000);

		var exception = Assert.Throws<InvalidOperationException>(() =>
			((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context));

		Assert.Contains("StreamKey is required", exception.Message);
		Assert.Contains("WithStreamKey", exception.Message);
	}

	[Fact]
	public void Build_WithoutSerializer_ThrowsInvalidOperationException()
	{
		_builder.WithStreamKey("test:stream")
			.WithMaxLength(1000);

		var exception = Assert.Throws<InvalidOperationException>(() =>
			((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context));

		Assert.Contains("Serializer is required", exception.Message);
		Assert.Contains("WithSerializer", exception.Message);
	}

	[Fact]
	public void Build_WithoutMaxLength_ThrowsInvalidOperationException()
	{
		_builder.WithStreamKey("test:stream")
			.WithSerializer(_serializer);

		var exception = Assert.Throws<InvalidOperationException>(() =>
			((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context));

		Assert.Contains("MaxLength is required", exception.Message);
		Assert.Contains("WithMaxLength", exception.Message);
	}

	[Fact]
	public void Build_WithNullContext_ThrowsArgumentNullException()
	{
		_builder.WithStreamKey("test:stream")
			.WithSerializer(_serializer)
			.WithMaxLength(1000);

		Assert.Throws<ArgumentNullException>(() =>
			((IStreamAdapterBuilder<TestMessage>)_builder).Build(null!));
	}

	[Fact]
	public void Build_CalledTwice_CreatesDifferentInstances()
	{
		_builder.WithStreamKey("test:stream")
			.WithSerializer(_serializer)
			.WithMaxLength(1000);

		var adapter1 = ((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context);
		var adapter2 = ((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context);

		Assert.NotNull(adapter1);
		Assert.NotNull(adapter2);
		Assert.NotSame(adapter1, adapter2);
	}

	[Fact]
	public void FluentApi_AllMethods_ReturnBuilder()
	{
		var result = _builder
			.WithStreamKey("test:stream")
			.WithSerializer(_serializer)
			.WithMaxLength(1000);

		Assert.Same(_builder, result);
	}

	[Fact]
	public void Build_WithMinimumMaxLength_CreatesAdapter()
	{
		_builder.WithStreamKey("test:stream")
			.WithSerializer(_serializer)
			.WithMaxLength(1);

		var adapter = ((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context);

		Assert.NotNull(adapter);
	}

	[Fact]
	public void Build_WithLargeMaxLength_CreatesAdapter()
	{
		_builder.WithStreamKey("test:stream")
			.WithSerializer(_serializer)
			.WithMaxLength(1000000);

		var adapter = ((IStreamAdapterBuilder<TestMessage>)_builder).Build(_context);

		Assert.NotNull(adapter);
	}
}
