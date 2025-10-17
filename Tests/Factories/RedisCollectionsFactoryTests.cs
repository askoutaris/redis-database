using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase.Factories;
using RedisDatabase.PeriodicTriggers;
using StackExchange.Redis;

namespace Tests.Factories;

public class RedisCollectionsFactoryTests
{
	private readonly IConnectionMultiplexer _multiplexer;
	private readonly IPeriodicTriggerFactory _triggerFactory;
	private readonly IPeriodicTrigger _trigger;
	private readonly ILoggerFactory _loggerFactory;

	public RedisCollectionsFactoryTests()
	{
		_multiplexer = Substitute.For<IConnectionMultiplexer>();
		_triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		_trigger = Substitute.For<IPeriodicTrigger>();
		_loggerFactory = Substitute.For<ILoggerFactory>();

		_triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(_trigger);
	}

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		var factory = new RedisCollectionsFactory(_multiplexer, _triggerFactory, _loggerFactory);

		Assert.NotNull(factory);
		_triggerFactory.Received(1).Create<Arg.AnyType>(TimeSpan.FromSeconds(5));
	}
}
