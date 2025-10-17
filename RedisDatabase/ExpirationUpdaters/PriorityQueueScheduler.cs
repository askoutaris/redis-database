using System.Collections.Concurrent;

namespace RedisDatabase.ExpirationUpdaters
{
	interface IPriorityQueueScheduler<T> where T : notnull
	{
		void ScheduleOrUpdate(T item, TimeSpan expiration);
		IReadOnlyCollection<KeyValuePair<T, TimeSpan>> GetScheduleds(DateTime referenceTime);
	}

	class PriorityQueueScheduler<T> : IPriorityQueueScheduler<T> where T : notnull
	{
		private readonly PriorityQueue<T, DateTime> _scheduledUpdates;
		private readonly ConcurrentDictionary<T, ScheduledEntry> _scheduledExpirations;
		private readonly object _lock;

		public PriorityQueueScheduler()
		{
			_scheduledUpdates = new PriorityQueue<T, DateTime>();
			_scheduledExpirations = [];
			_lock = new();
		}

		public void ScheduleOrUpdate(T item, TimeSpan expiration)
		{
			// Fast path: If key already scheduled, just update expiration
			if (TryScheduleOrUpdateKey(item, expiration, false))
				return;

			// PriorityQueue is not thread-safe
			lock (_lock)
				TryScheduleOrUpdateKey(item, expiration, true);
		}

		public IReadOnlyCollection<KeyValuePair<T, TimeSpan>> GetScheduleds(DateTime referenceTime)
		{
			var dueUpdates = new List<KeyValuePair<T, TimeSpan>>();

			// PriorityQueue is not thread-safe
			lock (_lock)
			{
				// Dequeue all entries that are due
				while (_scheduledUpdates.Count > 0 &&
					_scheduledUpdates.TryPeek(out _, out var scheduledTime) &&
					scheduledTime <= referenceTime)
				{
					var key = _scheduledUpdates.Dequeue();

					// Get the LATEST expiration value from dictionary
					if (_scheduledExpirations.TryRemove(key, out var touchedKey))
					{
						var slidingExpiration = touchedKey.GetSlidingExpiration(referenceTime);

						dueUpdates.Add(new(key, slidingExpiration));

						if (touchedKey.LastAccess.HasValue)
						{
							// Re-schedule for half-life of slidingExpiration from NOW in order to throttle scheduling too many updates if we access the key multiple times again
							var nextUpdate = referenceTime + (slidingExpiration / 2);

							// Keep in _scheduledExpirations (still scheduled)
							_scheduledUpdates.Enqueue(key, nextUpdate);
							_scheduledExpirations[key] = new ScheduledEntry(
								expiration: touchedKey.Expiration,
								lastAccess: null);
						}
					}
				}
			}

			return dueUpdates;
		}

		private bool TryScheduleOrUpdateKey(T item, TimeSpan expiration, bool write)
		{
			var now = DateTime.UtcNow;

			if (_scheduledExpirations.TryGetValue(item, out _))
			{
				_scheduledExpirations[item] = new ScheduledEntry(
					expiration: expiration,
					lastAccess: now);

				return true;
			}

			if (write)
			{
				// First time seeing this key - schedule for immediate update to cover the case we access the key just before its expiration
				_scheduledUpdates.Enqueue(item, now);

				_scheduledExpirations[item] = new ScheduledEntry(
					expiration: expiration,
					lastAccess: now);

				return true;
			}

			return false;
		}
	}

	readonly struct ScheduledEntry
	{
		public TimeSpan Expiration { get; }
		public DateTime? LastAccess { get; }

		public ScheduledEntry(TimeSpan expiration, DateTime? lastAccess)
		{
			Expiration = expiration;
			LastAccess = lastAccess;
		}

		public TimeSpan GetSlidingExpiration(DateTime referenceTime)
		{
			var passedTime = LastAccess.HasValue
				? referenceTime - LastAccess.Value
				: TimeSpan.Zero;

			var slidingExpiration = Expiration - passedTime;

			return slidingExpiration;
		}
	}
}
