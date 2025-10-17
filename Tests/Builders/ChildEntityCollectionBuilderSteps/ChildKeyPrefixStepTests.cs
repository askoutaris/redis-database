using RedisDatabase.Builders;
using RedisDatabase.Builders.ChildEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ChildEntityCollectionBuilderSteps;

public class ChildKeyPrefixStepTests
{
	private readonly IChildEntityCollectionBuilder<int, string, TestEntity> _builder;

	public ChildKeyPrefixStepTests()
	{
		_builder = Substitute.For<IChildEntityCollectionBuilder<int, string, TestEntity>>();
		_builder.WithChildKeyPrefix(Arg.Any<string>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithChildKeyPrefix_WithValidPrefix_CallsBuilderAndReturnsSerializerStep()
	{
		var step = new ChildKeyPrefixStep<int, string, TestEntity>(_builder);

		var result = step.WithChildKeyPrefix("child");

		_builder.Received(1).WithChildKeyPrefix("child");
		Assert.NotNull(result);
		Assert.IsAssignableFrom<ISerializerStep<int, string, TestEntity>>(result);
	}

	[Fact]
	public void WithChildKeyPrefix_WithNullPrefix_ThrowsArgumentNullException()
	{
		var step = new ChildKeyPrefixStep<int, string, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithChildKeyPrefix(null!));
	}
}
