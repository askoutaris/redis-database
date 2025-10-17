using RedisDatabase.Builders;
using RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ConcurrentEntityCollectionBuilderSteps;

public class NewConcurrencyTokenSelectorStepTests
{
	private readonly IConcurrentEntityCollectionBuilder<int, TestEntity> _builder;
	private readonly Func<TestEntity, string> _newConcurrencyTokenSelector;

	public NewConcurrencyTokenSelectorStepTests()
	{
		_builder = Substitute.For<IConcurrentEntityCollectionBuilder<int, TestEntity>>();
		_newConcurrencyTokenSelector = entity => entity.NewVersion;
		_builder.WithNewConcurrencyTokenSelector(Arg.Any<Func<TestEntity, string>>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
		public string NewVersion { get; set; } = string.Empty;
	}

	[Fact]
	public void WithNewConcurrencyTokenSelector_WithValidSelector_CallsBuilderAndReturnsBuilder()
	{
		var step = new NewConcurrencyTokenSelectorStep<int, TestEntity>(_builder);

		var result = step.WithNewConcurrencyTokenSelector(_newConcurrencyTokenSelector);

		_builder.Received(1).WithNewConcurrencyTokenSelector(_newConcurrencyTokenSelector);
		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithNewConcurrencyTokenSelector_WithNullSelector_ThrowsArgumentNullException()
	{
		var step = new NewConcurrencyTokenSelectorStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithNewConcurrencyTokenSelector(null!));
	}
}
