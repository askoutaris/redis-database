using RedisDatabase.Builders;
using RedisDatabase.Builders.ConcurrentEntityCollectionBuilderSteps;
using RedisDatabase.LifetimeProvider;
using NSubstitute;

namespace Tests.Builders.ConcurrentEntityCollectionBuilderSteps;

public class LifetimeProviderStepTests
{
	private readonly IConcurrentEntityCollectionBuilder<int, TestEntity> _builder;
	private readonly ILifetimeProvider _lifetimeProvider;

	public LifetimeProviderStepTests()
	{
		_builder = Substitute.For<IConcurrentEntityCollectionBuilder<int, TestEntity>>();
		_lifetimeProvider = Substitute.For<ILifetimeProvider>();
		_builder.WithCustomLifetimeProvider(Arg.Any<ILifetimeProvider>()).Returns(_builder);
		_builder.WithDefaultLifetimeProvider(Arg.Any<TimeSpan>(), Arg.Any<bool>()).Returns(_builder);
	}

	public class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	[Fact]
	public void WithCustomLifetimeProvider_WithValidProvider_CallsBuilderAndReturnsUniqueKeyFactoryStep()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);

		var result = step.WithCustomLifetimeProvider(_lifetimeProvider);

		_builder.Received(1).WithCustomLifetimeProvider(_lifetimeProvider);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<IUniqueKeyFactoryStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithCustomLifetimeProvider_WithNullProvider_ThrowsArgumentNullException()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);

		Assert.Throws<ArgumentNullException>(() => step.WithCustomLifetimeProvider(null!));
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithValidExpiration_CallsBuilderAndReturnsUniqueKeyFactoryStep()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);
		var expiration = TimeSpan.FromMinutes(5);

		var result = step.WithDefaultLifetimeProvider(expiration);

		_builder.Received(1).WithDefaultLifetimeProvider(expiration, false);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<IUniqueKeyFactoryStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithExpirationAndExtendOnReads_CallsBuilderAndReturnsUniqueKeyFactoryStep()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);
		var expiration = TimeSpan.FromMinutes(10);

		var result = step.WithDefaultLifetimeProvider(expiration, true);

		_builder.Received(1).WithDefaultLifetimeProvider(expiration, true);
		Assert.NotNull(result);
		Assert.IsAssignableFrom<IUniqueKeyFactoryStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithZeroExpiration_ThrowsArgumentOutOfRangeException()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			step.WithDefaultLifetimeProvider(TimeSpan.Zero));

		Assert.Equal("expiration", exception.ParamName);
	}

	[Fact]
	public void WithDefaultLifetimeProvider_WithNegativeExpiration_ThrowsArgumentOutOfRangeException()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);

		var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
			step.WithDefaultLifetimeProvider(TimeSpan.FromMinutes(-1)));

		Assert.Equal("expiration", exception.ParamName);
	}

	[Fact]
	public void WithNoExpiration_ReturnsUniqueKeyFactoryStep()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);

		var result = step.WithNoExpiration();

		Assert.IsType<UniqueKeyFactoryStep<int, TestEntity>>(result);
	}

	[Fact]
	public void WithNoExpiration_CallsBuilderWithNoExpiration()
	{
		var step = new LifetimeProviderStep<int, TestEntity>(_builder);

		step.WithNoExpiration();

		_builder.Received(1).WithNoExpiration();
	}
}
