using RedisDatabase;
using RedisDatabase.Collections;
using RedisDatabase.Factories;
using StackExchange.Redis;
using Workbench.Scenarios.Shared;

namespace Workbench.Scenarios.RedisDatabase
{
	public class RedisDBContext
	{
		private readonly RedisContext _context;

		public IEntityCollection<int, Person> People { get; set; }
		public IChildEntityCollection<int, int, Address> Addresses { get; set; }

		public RedisDBContext(IConnectionMultiplexer multiplexer, IRedisCollectionsFactory factory)
		{
			var db = multiplexer.GetDatabase();

			_context = new RedisContext(db);

			People = factory.GetConcurrentEntityCollection<int, Person>(_context);
			Addresses = factory.GetChildEntityCollection<int, int, Address>(_context);
		}

		public Task ExecuteReads()
			=> _context.ExecuteBatch();

		public Task SaveChanges()
			=> _context.Commit();
	}
}
