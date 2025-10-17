using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RedisDatabase.Extensions.DependencyInjection;
using RedisDatabase.Factories;
using RedisDatabase.PeriodicTriggers;
using StackExchange.Redis;

namespace Tests;

public class ServiceCollectionExtensionsTests
{
	[Fact]
	public void AddRedisDatabase_WithValidConfiguration_RegistersCollectionsFactory()
	{
		var services = new ServiceCollection();
		var multiplexer = Substitute.For<IConnectionMultiplexer>();
		var triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		var trigger = Substitute.For<IPeriodicTrigger>();
		var loggerFactory = Substitute.For<ILoggerFactory>();

		services.AddSingleton(multiplexer);
		services.AddSingleton(triggerFactory);
		services.AddSingleton(loggerFactory);
		triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(trigger);

		var configureWasCalled = false;
		services.AddRedisDatabase((sp, factory) =>
		{
			configureWasCalled = true;
			Assert.NotNull(sp);
			Assert.NotNull(factory);
		});

		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IRedisCollectionsFactory>();

		Assert.NotNull(factory);
		Assert.True(configureWasCalled);
	}

	[Fact]
	public void AddRedisDatabase_RegistersFactoryAsSingleton()
	{
		var services = new ServiceCollection();
		var multiplexer = Substitute.For<IConnectionMultiplexer>();
		var triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		var trigger = Substitute.For<IPeriodicTrigger>();
		var loggerFactory = Substitute.For<ILoggerFactory>();

		services.AddSingleton(multiplexer);
		services.AddSingleton(triggerFactory);
		services.AddSingleton(loggerFactory);
		triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(trigger);

		services.AddRedisDatabase((sp, factory) => { });

		var provider = services.BuildServiceProvider();
		var factory1 = provider.GetRequiredService<IRedisCollectionsFactory>();
		var factory2 = provider.GetRequiredService<IRedisCollectionsFactory>();

		Assert.Same(factory1, factory2);
	}

	[Fact]
	public void AddRedisDatabase_ReturnsServiceCollection()
	{
		var services = new ServiceCollection();
		var multiplexer = Substitute.For<IConnectionMultiplexer>();
		var triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		var trigger = Substitute.For<IPeriodicTrigger>();
		var loggerFactory = Substitute.For<ILoggerFactory>();

		services.AddSingleton(multiplexer);
		services.AddSingleton(triggerFactory);
		services.AddSingleton(loggerFactory);
		triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(trigger);

		var result = services.AddRedisDatabase((sp, factory) => { });

		Assert.Same(services, result);
	}

	[Fact]
	public void AddRedisDatabase_CallsPeriodicTriggerRegistration()
	{
		var services = new ServiceCollection();
		var multiplexer = Substitute.For<IConnectionMultiplexer>();
		var triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		var trigger = Substitute.For<IPeriodicTrigger>();
		var loggerFactory = Substitute.For<ILoggerFactory>();

		services.AddSingleton(multiplexer);
		services.AddSingleton(triggerFactory);
		services.AddSingleton(loggerFactory);
		triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(trigger);

		services.AddRedisDatabase((sp, factory) => { });

		var provider = services.BuildServiceProvider();
		var registeredTriggerFactory = provider.GetService<IPeriodicTriggerFactory>();

		Assert.NotNull(registeredTriggerFactory);
	}

	[Fact]
	public void AddRedisDatabase_PassesFactoryToConfigureAction()
	{
		var services = new ServiceCollection();
		var multiplexer = Substitute.For<IConnectionMultiplexer>();
		var triggerFactory = Substitute.For<IPeriodicTriggerFactory>();
		var trigger = Substitute.For<IPeriodicTrigger>();
		var loggerFactory = Substitute.For<ILoggerFactory>();

		services.AddSingleton(multiplexer);
		services.AddSingleton(triggerFactory);
		services.AddSingleton(loggerFactory);
		triggerFactory.Create<Arg.AnyType>(Arg.Any<TimeSpan>()).Returns(trigger);

		IRedisCollectionsFactory? capturedFactory = null;
		services.AddRedisDatabase((sp, factory) =>
		{
			capturedFactory = factory;
		});

		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IRedisCollectionsFactory>();

		Assert.NotNull(capturedFactory);
		Assert.Same(factory, capturedFactory);
	}

	[Fact]
	public void AddRedisDatabase_WithoutConnectionMultiplexer_ThrowsException()
	{
		var services = new ServiceCollection();
		var triggerFactory = Substitute.For<IPeriodicTriggerFactory>();

		services.AddSingleton(triggerFactory);

		services.AddRedisDatabase((sp, factory) => { });

		var provider = services.BuildServiceProvider();

		Assert.Throws<InvalidOperationException>(() =>
			provider.GetRequiredService<IRedisCollectionsFactory>());
	}
}
