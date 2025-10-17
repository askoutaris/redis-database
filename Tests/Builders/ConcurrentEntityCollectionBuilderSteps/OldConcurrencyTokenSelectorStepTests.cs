using RedisDatabase.Builders;
using RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ConcurrentEntityCollectionBuilderSteps;

public class OldConcurrencyTokenSelectorStepTests
{
	private readonly IConcurrentEntityCollectionBuilder<int, TestEntity> _builder;
	private readonly Func<TestEntity, string?> _oldConcurrencyTokenSelector;

	public OldConcurrencyTokenSelectorStepTests()
	{
		_builder = Substitute.For<IConcurrentEntityCollectionBuilder<int, TestEntity>>();
		_oldConcurrencyTokenSelector = entity => entity.OldVersion;
		_builder.WithOldConcurrencyTokenSelector(Arg.Any<Func<TestEntity, string?>>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
		public string? OldVersion { get; set; }
	}

	[Fact]
	public void WithOldConcurrencyTokenSelector_WithValidSelector_CallsBuilderAndReturnsNewConcurrencyTokenSelectorStep()
	{
		var step = new OldConcurrencyTokenSelectorStep<int, TestEntity>(_builder);

		var result = step.WithOldConcurrencyTokenSelector(_oldConcurrencyTokenSelector);

		_builder.Received(1).WithOldConcurrencyTokenSelector(_oldConcurrencyTokenSelector);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<INewConcurrencyTokenSelectorStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithOldConcurrencyTokenSelector_WithNullSelector_ThrowsArgumentNullException()
	{
		var step = new OldConcurrencyTokenSelectorStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithOldConcurrencyTokenSelector(null!));
	}
}
