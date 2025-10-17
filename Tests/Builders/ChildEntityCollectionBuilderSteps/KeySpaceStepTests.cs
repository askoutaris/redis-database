using RedisDatabase.Builders;
using RedisDatabase.Builders.ChildEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ChildEntityCollectionBuilderSteps;

public class KeySpaceStepTests
{
	private readonly IChildEntityCollectionBuilder<int, string, TestEntity> _builder;

	public KeySpaceStepTests()
	{
		_builder = Substitute.For<IChildEntityCollectionBuilder<int, string, TestEntity>>();
		_builder.WithKeySpace(Arg.Any<string>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithKeySpace_WithValidKeySpace_CallsBuilderAndReturnsChildKeyPrefixStep()
	{
		var step = new KeySpaceStep<int, string, TestEntity>(_builder);

		var result = step.WithKeySpace("test:parent");

		_builder.Received(1).WithKeySpace("test:parent");
		Assert.NotNull(result);
		Assert.IsAssignableFrom<IChildKeyPrefixStep<int, string, TestEntity>>(result);
	}

	[Fact]
	public void WithKeySpace_WithNullKeySpace_ThrowsArgumentException()
	{
		var step = new KeySpaceStep<int, string, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithKeySpace(null!));
	}

	[Fact]
	public void WithKeySpace_WithEmptyKeySpace_ThrowsArgumentException()
	{
		var step = new KeySpaceStep<int, string, TestEntity>(_builder);

		Assert.Throws<ArgumentException>(() => step.WithKeySpace(string.Empty));
	}

	[Fact]
	public void WithKeySpace_WithWhitespaceKeySpace_ThrowsArgumentException()
	{
		var step = new KeySpaceStep<int, string, TestEntity>(_builder);

		Assert.Throws<ArgumentException>(() => step.WithKeySpace("   "));
	}
}
