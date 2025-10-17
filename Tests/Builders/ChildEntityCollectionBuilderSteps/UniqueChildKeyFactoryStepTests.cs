using RedisDatabase.Builders;
using RedisDatabase.Builders.ChildEntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.ChildEntityCollectionBuilderSteps;

public class UniqueChildKeyFactoryStepTests
{
	private readonly IChildEntityCollectionBuilder<int, string, TestEntity> _builder;
	private readonly Func<string, string> _uniqueChildKeyFactory;

	public UniqueChildKeyFactoryStepTests()
	{
		_builder = Substitute.For<IChildEntityCollectionBuilder<int, string, TestEntity>>();
		_uniqueChildKeyFactory = childKey => childKey;
		_builder.WithUniqueChildKeyFactory(Arg.Any<Func<string, string>>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithUniqueChildKeyFactory_WithValidFactory_CallsBuilderAndReturnsBuilder()
	{
		var step = new UniqueChildKeyFactoryStep<int, string, TestEntity>(_builder);

		var result = step.WithUniqueChildKeyFactory(_uniqueChildKeyFactory);

		_builder.Received(1).WithUniqueChildKeyFactory(_uniqueChildKeyFactory);
		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithUniqueChildKeyFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		var step = new UniqueChildKeyFactoryStep<int, string, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithUniqueChildKeyFactory(null!));
	}
}
