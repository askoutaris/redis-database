using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedisDatabase.Extensions.DependencyInjection;
using StackExchange.Redis;
using Workbench.Scenarios.Shared;

namespace Workbench.Scenarios.RedisDatabase
{
	class RedisDatabaseScenario : IScenario
	{
		private readonly IHost _host;

		public RedisDatabaseScenario(IHost host)
		{
			_host = host;
		}

		public void Start()
		{
			Task.Run(async () =>
			{
				await AddEntriesAsync();
				await ReadEntries();
			});
		}

		public void Stop()
		{
		}

		private async Task AddEntriesAsync()
		{
			var db = _host.Services.GetRequiredService<RedisDBContext>();
			var person1 = new Person(1, 0, "Alkis");
			db.People.Set(person1.Id, person1);
			await db.SaveChanges();


			db = _host.Services.GetRequiredService<RedisDBContext>();
			var read = db.People.TryRead(person1.Id);
			await db.ExecuteReads();
			person1 = read.Value!;
			person1.Name = "Test";

			
			db = _host.Services.GetRequiredService<RedisDBContext>();
			db.People.Set(person1.Id, person1);
			await db.SaveChanges();

			db = _host.Services.GetRequiredService<RedisDBContext>();
			db.People.Set(person1.Id, person1);
			await db.SaveChanges();
		}

		private async Task ReadEntries()
		{
			var db = _host.Services.GetRequiredService<RedisDBContext>();

			var read1 = db.People.TryRead(1);
			var read2 = db.People.TryRead(2);

			await db.ExecuteReads();

			var p1 = read1.Value;
			var p2 = read2.Value;
		}

		public static void Configure(HostBuilderContext context, IServiceCollection services)
		{
			services.AddSingleton<IScenario, RedisDatabaseScenario>();

			services.AddRedisDatabase((ctx, factory) =>
			{
				factory.RegisterConcurrentEntityCollection<int, Person>(cfg => cfg
					.WithKeySpace("people")
					.WithSignaturedSerializer(new JsonRedisSerializer())
					.WithDefaultLifetimeProvider(TimeSpan.FromSeconds(600), true)
					.WithUniqueKeyFactory(id => id.ToString())
					.WithOldConcurrencyTokenSelector(x => x.Version == 0 ? null : x.Version.ToString())
					.WithNewConcurrencyTokenSelector(x => x.GetNextVersion().ToString())
				);

				factory.RegisterChildEntityCollection<int, int, Address>(cfg => cfg
					.WithKeySpace("people")
					.WithChildKeyPrefix("address")
					.WithSerializer(new JsonRedisSerializer())
					.WithDefaultLifetimeProvider(TimeSpan.FromSeconds(60), true)
					.WithUniqueParentKeyFactory(id => id.ToString())
					.WithUniqueChildKeyFactory(id => id.ToString())
				);
			});

			// Register Redis connection
			var redisConnectionString = context.Configuration["RedisConnectionString"]!;
			services.AddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(redisConnectionString));

			services.AddTransient<RedisDBContext>();
		}
	}
}
