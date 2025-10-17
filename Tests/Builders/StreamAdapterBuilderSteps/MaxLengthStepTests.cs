using RedisDatabase.Builders;
using RedisDatabase.Builders.StreamAdapterBuilderSteps;
using NSubstitute;

namespace Tests.Builders.StreamAdapterBuilderSteps;

public class MaxLengthStepTests
{
	private readonly IStreamAdapterBuilder<TestMessage> _builder;

	public MaxLengthStepTests()
	{
		_builder = Substitute.For<IStreamAdapterBuilder<TestMessage>>();
		_builder.WithMaxLength(Arg.Any<int>()).Returns(_builder);
	}

	public class TestMessage
	{
		public int Id { get; set; }
		public string Content { get; set; } = string.Empty;
	}

	[Fact]
	public void WithMaxLength_WithValidValue_CallsBuilderAndReturnsBuilder()
	{
		var step = new MaxLengthStep<TestMessage>(_builder);

		var result = step.WithMaxLength(1000);

		_builder.Received(1).WithMaxLength(1000);
		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithMaxLength_WithZero_ThrowsArgumentOutOfRangeException()
	{
		var step = new MaxLengthStep<TestMessage>(_builder);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => step.WithMaxLength(0));

		Assert.Equal("maxLength", exception.ParamName);
		Assert.Contains("MaxLength must be greater than 0", exception.Message);
	}

	[Fact]
	public void WithMaxLength_WithNegativeValue_ThrowsArgumentOutOfRangeException()
	{
		var step = new MaxLengthStep<TestMessage>(_builder);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => step.WithMaxLength(-1));

		Assert.Equal("maxLength", exception.ParamName);
	}

	[Fact]
	public void WithMaxLength_WithMinimumValue_CallsBuilder()
	{
		var step = new MaxLengthStep<TestMessage>(_builder);

		step.WithMaxLength(1);

		_builder.Received(1).WithMaxLength(1);
	}

	[Fact]
	public void WithMaxLength_WithLargeValue_CallsBuilder()
	{
		var step = new MaxLengthStep<TestMessage>(_builder);

		step.WithMaxLength(1000000);

		_builder.Received(1).WithMaxLength(1000000);
	}
}
