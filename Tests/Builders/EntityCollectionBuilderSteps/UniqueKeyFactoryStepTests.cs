using RedisDatabase.Builders;
using RedisDatabase.Builders.EntityCollectionBuilderSteps;
using NSubstitute;

namespace Tests.Builders.EntityCollectionBuilderSteps;

public class UniqueKeyFactoryStepTests
{
	private readonly IEntityCollectionBuilder<int, TestEntity> _builder;
	private readonly Func<int, string> _uniqueKeyFactory;

	public UniqueKeyFactoryStepTests()
	{
		_builder = Substitute.For<IEntityCollectionBuilder<int, TestEntity>>();
		_uniqueKeyFactory = key => key.ToString();
		_builder.WithUniqueKeyFactory(Arg.Any<Func<int, string>>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithUniqueKeyFactory_WithValidFactory_CallsBuilderAndReturnsBuilder()
	{
		var step = new UniqueKeyFactoryStep<int, TestEntity>(_builder);

		var result = step.WithUniqueKeyFactory(_uniqueKeyFactory);

		_builder.Received(1).WithUniqueKeyFactory(_uniqueKeyFactory);
		Assert.NotNull(result);
		Assert.Same(_builder, result);
	}

	[Fact]
	public void WithUniqueKeyFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		var step = new UniqueKeyFactoryStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithUniqueKeyFactory(null!));
	}
}
