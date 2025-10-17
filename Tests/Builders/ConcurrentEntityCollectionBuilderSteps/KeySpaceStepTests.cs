using RedisDatabase.Builders;
using RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ConcurrentEntityCollectionBuilderSteps;

public class KeySpaceStepTests
{
	private readonly IConcurrentEntityCollectionBuilder<int, TestEntity> _builder;

	public KeySpaceStepTests()
	{
		_builder = Substitute.For<IConcurrentEntityCollectionBuilder<int, TestEntity>>();
		_builder.WithKeySpace(Arg.Any<string>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithKeySpace_WithValidKeySpace_CallsBuilderAndReturnsSerializerStep()
	{
		var step = new KeySpaceStep<int, TestEntity>(_builder);

		var result = step.WithKeySpace("test:entities");

		_builder.Received(1).WithKeySpace("test:entities");
		Assert.NotNull(result);
		Assert.IsAssignableFrom<ISerializerStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithKeySpace_WithNullKeySpace_ThrowsArgumentException()
	{
		var step = new KeySpaceStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithKeySpace(null!));
	}

	[Fact]
	public void WithKeySpace_WithEmptyKeySpace_ThrowsArgumentException()
	{
		var step = new KeySpaceStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentException>(() => step.WithKeySpace(string.Empty));
	}

	[Fact]
	public void WithKeySpace_WithWhitespaceKeySpace_ThrowsArgumentException()
	{
		var step = new KeySpaceStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentException>(() => step.WithKeySpace("   "));
	}
}
