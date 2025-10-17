using RedisDatabase.Builders;
using RedisDatabase.Builders.StreamAdapterBuilderSteps;
using RedisDatabase.Serializers;
using NSubstitute;

namespace Tests.Builders.StreamAdapterBuilderSteps;

public class SerializerStepTests
{
	private readonly IStreamAdapterBuilder<TestMessage> _builder;
	private readonly IRedisSerializer _serializer;

	public SerializerStepTests()
	{
		_builder = Substitute.For<IStreamAdapterBuilder<TestMessage>>();
		_serializer = Substitute.For<IRedisSerializer>();
		_builder.WithSerializer(Arg.Any<IRedisSerializer>()).Returns(_builder);
	}

	public class TestMessage
	{
		public int Id { get; set; }
		public string Content { get; set; } = string.Empty;
	}

	[Fact]
	public void WithSerializer_WithValidSerializer_CallsBuilderAndReturnsMaxLengthStep()
	{
		var step = new SerializerStep<TestMessage>(_builder);

		var result = step.WithSerializer(_serializer);

		_builder.Received(1).WithSerializer(_serializer);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<IMaxLengthStep<TestMessage>>(result);
	}

	[Fact]
	public void WithSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var step = new SerializerStep<TestMessage>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithSerializer(null!));
	}

	[Fact]
	public void WithSerializer_CallsBuilderOnce()
	{
		var step = new SerializerStep<TestMessage>(_builder);

		step.WithSerializer(_serializer);

		_builder.Received(1).WithSerializer(_serializer);
	}
}
