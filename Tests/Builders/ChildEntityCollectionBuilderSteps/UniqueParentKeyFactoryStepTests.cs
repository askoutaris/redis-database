using RedisDatabase.Builders;
using RedisDatabase.Builders.ChildEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ChildEntityCollectionBuilderSteps;

public class UniqueParentKeyFactoryStepTests
{
	private readonly IChildEntityCollectionBuilder<int, string, TestEntity> _builder;
	private readonly Func<int, string> _uniqueParentKeyFactory;

	public UniqueParentKeyFactoryStepTests()
	{
		_builder = Substitute.For<IChildEntityCollectionBuilder<int, string, TestEntity>>();
		_uniqueParentKeyFactory = parentKey => parentKey.ToString();
		_builder.WithUniqueParentKeyFactory(Arg.Any<Func<int, string>>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithUniqueParentKeyFactory_WithValidFactory_CallsBuilderAndReturnsUniqueChildKeyFactoryStep()
	{
		var step = new UniqueParentKeyFactoryStep<int, string, TestEntity>(_builder);

		var result = step.WithUniqueParentKeyFactory(_uniqueParentKeyFactory);

		_builder.Received(1).WithUniqueParentKeyFactory(_uniqueParentKeyFactory);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<IUniqueChildKeyFactoryStep<int, string, TestEntity>>(result);
	}

	[Fact]
	public void WithUniqueParentKeyFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		var step = new UniqueParentKeyFactoryStep<int, string, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithUniqueParentKeyFactory(null!));
	}
}
