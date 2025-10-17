using Microsoft.Extensions.Hosting;
using Workbench.Scenarios;

namespace Workbench
{
	class StartupHostedService : IHostedService
	{
		private readonly IScenario _scenario;

		public StartupHostedService(IScenario scenario)
		{
			_scenario = scenario;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_scenario.Start();

			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_scenario.Stop();

			return Task.CompletedTask;
		}
	}
}
