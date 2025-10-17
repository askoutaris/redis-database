using RedisDatabase.Builders;
using RedisDatabase.Builders.StreamAdapterBuilderSteps;
using NSubstitute;

namespace Tests.Builders.StreamAdapterBuilderSteps;

public class StreamKeyStepTests
{
	private readonly IStreamAdapterBuilder<TestMessage> _builder;

	public StreamKeyStepTests()
	{
		_builder = Substitute.For<IStreamAdapterBuilder<TestMessage>>();
		_builder.WithStreamKey(Arg.Any<string>()).Returns(_builder);
	}

	public class TestMessage
	{
		public int Id { get; set; }
		public string Content { get; set; } = string.Empty;
	}

	[Fact]
	public void WithStreamKey_WithValidKey_CallsBuilderAndReturnsSerializerStep()
	{
		var step = new StreamKeyStep<TestMessage>(_builder);

		var result = step.WithStreamKey("test:stream");

		_builder.Received(1).WithStreamKey("test:stream");
		Assert.NotNull(result);
		Assert.IsAssignableFrom<ISerializerStep<TestMessage>>(result);
	}

	[Fact]
	public void WithStreamKey_WithNullKey_ThrowsArgumentException()
	{
		var step = new StreamKeyStep<TestMessage>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithStreamKey(null!));
	}

	[Fact]
	public void WithStreamKey_WithEmptyKey_ThrowsArgumentException()
	{
		var step = new StreamKeyStep<TestMessage>(_builder);

		Assert.Throws<ArgumentException>(() => step.WithStreamKey(string.Empty));
	}

	[Fact]
	public void WithStreamKey_WithWhitespaceKey_ThrowsArgumentException()
	{
		var step = new StreamKeyStep<TestMessage>(_builder);

		Assert.Throws<ArgumentException>(() => step.WithStreamKey("   "));
	}

	[Fact]
	public void WithStreamKey_CallsBuilderOnce()
	{
		var step = new StreamKeyStep<TestMessage>(_builder);

		step.WithStreamKey("my:stream");

		_builder.Received(1).WithStreamKey("my:stream");
	}
}
