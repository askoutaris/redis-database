using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedisDatabase.Factories;
using RedisDatabase.PeriodicTriggers;
using StackExchange.Redis;

namespace RedisDatabase.Extensions.DependencyInjection
{
	/// <summary>
	/// Provides extension methods for registering Redis database services in the dependency injection container.
	/// </summary>
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the Redis database services including the collections factory with periodic background expiration updates.
		/// Requires <see cref="IConnectionMultiplexer"/> to be registered in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add services to.</param>
		/// <param name="configure">Configuration action to register collections using the factory during startup.</param>
		/// <returns>The service collection for chaining.</returns>
		public static IServiceCollection AddRedisDatabase(this IServiceCollection services, Action<IServiceProvider, IRedisCollectionsFactory> configure)
		{
			services.AddSingleton<IPeriodicTriggerFactory, PeriodicTriggerFactory>();

			services.AddSingleton<IRedisCollectionsFactory>(ctx =>
			{
				var multiplexer = ctx.GetRequiredService<IConnectionMultiplexer>();
				var triggerFactory = ctx.GetRequiredService<IPeriodicTriggerFactory>();
				var loggerFactory = ctx.GetRequiredService<ILoggerFactory>();
				var factory = new RedisCollectionsFactory(multiplexer, triggerFactory, loggerFactory);

				configure(ctx, factory);

				return factory;
			});

			return services;
		}
	}
}