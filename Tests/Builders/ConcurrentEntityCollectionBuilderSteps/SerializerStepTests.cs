using RedisDatabase.Builders;
using RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps;
using RedisDatabase.Serializers;
using NSubstitute;

namespace Tests.Builders.ConcurrentEntityCollectionBuilderSteps;

public class SerializerStepTests
{
	private readonly IConcurrentEntityCollectionBuilder<int, TestEntity> _builder;
	private readonly IRedisSerializer _serializer;

	public SerializerStepTests()
	{
		_builder = Substitute.For<IConcurrentEntityCollectionBuilder<int, TestEntity>>();
		_serializer = Substitute.For<IRedisSerializer>();
		_builder.WithSerializer(Arg.Any<IRedisSerializer>()).Returns(_builder);
		_builder.WithSignaturedSerializer(Arg.Any<IRedisSerializer>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithSerializer_WithValidSerializer_CallsBuilderAndReturnsLifetimeProviderStep()
	{
		var step = new SerializerStep<int, TestEntity>(_builder);

		var result = step.WithSerializer(_serializer);

		_builder.Received(1).WithSerializer(_serializer);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<ILifetimeProviderStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var step = new SerializerStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithSerializer(null!));
	}

	[Fact]
	public void WithSignaturedSerializer_WithValidSerializer_CallsBuilderAndReturnsLifetimeProviderStep()
	{
		var step = new SerializerStep<int, TestEntity>(_builder);

		var result = step.WithSignaturedSerializer(_serializer);

		_builder.Received(1).WithSignaturedSerializer(_serializer);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<ILifetimeProviderStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithSignaturedSerializer_WithNullSerializer_ThrowsArgumentNullException()
	{
		var step = new SerializerStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithSignaturedSerializer(null!));
	}
}
