using RedisDatabase.PeriodicTriggers;
using StackExchange.Redis;

namespace RedisDatabase.ExpirationUpdaters
{
	class BackgroundExpirationUpdater : IExpirationUpdater
	{
		private readonly IPriorityQueueScheduler<RedisKey> _scheduler;
		private readonly IConnectionMultiplexer _multiplexer;
		private readonly IPeriodicTrigger _trigger;

		public BackgroundExpirationUpdater(IConnectionMultiplexer multiplexer, IPriorityQueueScheduler<RedisKey> scheduler, IPeriodicTrigger trigger)
		{
			_multiplexer = multiplexer;
			_scheduler = scheduler;
			_trigger = trigger;
			_trigger.OnTrigger += UpdateLifetimes;
		}

		public void UpdateLifetime(RedisKey key, TimeSpan? expiration)
		{
			if (expiration is null)
				return;

			_scheduler.ScheduleOrUpdate(key, expiration.Value);
		}

		private async Task UpdateLifetimes()
		{
			var dueUpdates = _scheduler.GetScheduleds(DateTime.UtcNow);

			// Process in chunks of 1000
			var chunks = dueUpdates.Chunk(1000).ToArray();

			foreach (var chunk in chunks)
			{
				var db = _multiplexer.GetDatabase();
				var context = new RedisContext(db);

				foreach (var pair in chunk)
					_ = context.AddBatch(db => db.KeyExpireAsync(pair.Key, pair.Value));

				await context.ExecuteBatch();
			}
		}
	}
}