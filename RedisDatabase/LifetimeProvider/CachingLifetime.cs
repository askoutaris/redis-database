using System.Diagnostics.CodeAnalysis;

namespace RedisDatabase.LifetimeProvider
{
	/// <summary>
	/// Represents the lifetime policy for cached entities, including expiration time and read extension behavior.
	/// </summary>
	[ExcludeFromCodeCoverage]
	public readonly struct CachingLifetime
	{
		/// <summary>
		/// Gets the expiration duration for the cached entity, or null for no expiration.
		/// </summary>
		public TimeSpan? Expiration { get; }

		/// <summary>
		/// Gets a value indicating whether the expiration time should be extended on read operations.
		/// When true, each read resets the expiration timer (sliding expiration).
		/// </summary>
		public bool ExtendExpirationOnReads { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CachingLifetime"/> struct.
		/// </summary>
		/// <param name="expiration">The expiration duration, or null for no expiration.</param>
		/// <param name="extendExpirationOnReads">Whether to extend expiration on reads (sliding expiration).</param>
		public CachingLifetime(TimeSpan? expiration, bool extendExpirationOnReads)
		{
			Expiration = expiration;
			ExtendExpirationOnReads = extendExpirationOnReads;
		}
	}
}
