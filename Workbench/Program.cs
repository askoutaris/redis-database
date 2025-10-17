using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;
using Workbench.Scenarios.RedisDatabase;

namespace Workbench
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			var builder = Host.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration((hostingContext, config) =>
				{
					config
						.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
						.AddEnvironmentVariables()
						.AddCommandLine(args);
				})
				.ConfigureLogging(logging =>
				{
					logging.ClearProviders();
					logging.SetMinimumLevel(LogLevel.Trace);
					logging.AddNLog("NLog.config");
				});

			builder.ConfigureServices((context, services) =>
			{
				SelectScenario(context, services);

				services.AddHostedService<StartupHostedService>();
			});

			var host = builder.Build();

			await host.RunAsync();
		}

		private static void SelectScenario(HostBuilderContext context, IServiceCollection services)
		{
			while (true)
			{
				Console.WriteLine("Enter scenario:");

				string? scenario = Console.ReadLine();

				switch (scenario?.ToLower())
				{
					case "redisdatabase": RedisDatabaseScenario.Configure(context, services); return;
					default:
						Console.WriteLine($"Invalid scenario: {scenario}");
						break;
				}
			}
		}
	}
}
