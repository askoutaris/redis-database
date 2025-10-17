using RedisDatabase.Adapters;
using RedisDatabase.Collections;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using RedisDatabase.Serializers.RedisSerializerSignatureDecorators;
using RedisDatabase.Serializers;
using TypeSignature;
using TypeSignature.HashGenerators;
using RedisDatabase.Exceptions;

namespace RedisDatabase.Builders
{
	/// <summary>
	/// Fluent builder for configuring concurrent entity collections with optimistic concurrency control using Redis hash storage.
	/// </summary>
	/// <typeparam name="TKey">The entity key type.</typeparam>
	/// <typeparam name="TEntity">The entity type to cache with concurrency control.</typeparam>
	public interface IConcurrentEntityCollectionBuilder<TKey, TEntity> : ICollectionBuilder
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>Configures the Redis hash key prefix for the collection.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithKeySpace(string keySpace);

		/// <summary>Configures the serializer for entity serialization/deserialization.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithSerializer(IRedisSerializer serializer);

		/// <summary>Configures the serializer with type signature validation to detect schema changes and invalidate incompatible cached data.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer);

		/// <summary>Configures a custom lifetime provider for key expiration management.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider lifetimeProvider);

		/// <summary>Configures a default lifetime provider with fixed expiration time and optional read extension.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false);

		/// <summary>Configures the collection to have no expiration (keys persist indefinitely).</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithNoExpiration();

		/// <summary>Configures the expiration updater strategy for key lifetime management.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithExpirationUpdater(IExpirationUpdater expirationUpdater);

		/// <summary>Configures the factory function to convert entity keys to unique Redis key strings.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> uniqueKeyFactory);

		/// <summary>Configures the selector to extract the existing concurrency token from entities for optimistic locking.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithOldConcurrencyTokenSelector(Func<TEntity, string?> oldConcurrencyTokenSelector);

		/// <summary>Configures the selector to generate new concurrency tokens for entities on updates.</summary>
		IConcurrentEntityCollectionBuilder<TKey, TEntity> WithNewConcurrencyTokenSelector(Func<TEntity, string> newConcurrencyTokenSelector);

		/// <summary>Builds the concurrent entity collection instance with the specified Redis context.</summary>
		internal IEntityCollection<TKey, TEntity> Build(IRedisContext context);
	}

	class ConcurrentEntityCollectionBuilder<TKey, TEntity> : IConcurrentEntityCollectionBuilder<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		private string? _keySpace;
		private IRedisSerializer? _serializer;
		private ILifetimeProvider? _lifetimeProvider;
		private IExpirationUpdater _expirationUpdater;
		private readonly ILoggerFactory _loggerFactory;
		private Func<TKey, string>? _uniqueKeyFactory;
		private Func<TEntity, string?>? _oldConcurrencyTokenSelector;
		private Func<TEntity, string>? _newConcurrencyTokenSelector;

		public ConcurrentEntityCollectionBuilder(IExpirationUpdater defaultExpirationUpdater, ILoggerFactory loggerFactory)
		{
			_expirationUpdater = defaultExpirationUpdater;
			_loggerFactory = loggerFactory;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithKeySpace(string keySpace)
		{
			ArgumentNullException.ThrowIfNull(keySpace, nameof(keySpace));

			_keySpace = keySpace;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_serializer = serializer;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			var typeScanner = new TypeScanner();
			var hashGenerator = new SHA256HashGenerator();
			var signatureBuilder = new SignatureBuilder(typeScanner, hashGenerator);
			var signatureProvider = new TypeSignatureProvider(signatureBuilder);
			var decorator = new RedisSerializerSignatureDecorator(serializer, signatureProvider);

			_serializer = decorator;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider lifetimeProvider)
		{
			ArgumentNullException.ThrowIfNull(lifetimeProvider, nameof(lifetimeProvider));

			_lifetimeProvider = lifetimeProvider;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false)
		{
			if (expiration <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero");

			_lifetimeProvider = new DefaultLifetimeProvider(expiration, extendExpirationOnReads);

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithNoExpiration()
		{
			_lifetimeProvider = new DefaultLifetimeProvider(null, false);

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithExpirationUpdater(IExpirationUpdater expirationUpdater)
		{
			ArgumentNullException.ThrowIfNull(expirationUpdater, nameof(expirationUpdater));

			_expirationUpdater = expirationUpdater;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> uniqueKeyFactory)
		{
			ArgumentNullException.ThrowIfNull(uniqueKeyFactory, nameof(uniqueKeyFactory));

			_uniqueKeyFactory = uniqueKeyFactory;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithOldConcurrencyTokenSelector(Func<TEntity, string?> oldConcurrencyTokenSelector)
		{
			ArgumentNullException.ThrowIfNull(oldConcurrencyTokenSelector, nameof(oldConcurrencyTokenSelector));

			_oldConcurrencyTokenSelector = oldConcurrencyTokenSelector;

			return this;
		}

		public IConcurrentEntityCollectionBuilder<TKey, TEntity> WithNewConcurrencyTokenSelector(Func<TEntity, string> newConcurrencyTokenSelector)
		{
			ArgumentNullException.ThrowIfNull(newConcurrencyTokenSelector, nameof(newConcurrencyTokenSelector));

			_newConcurrencyTokenSelector = newConcurrencyTokenSelector;

			return this;
		}

		public IEntityCollection<TKey, TEntity> Build(IRedisContext context)
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			ParameterNullException.ThrowIfNull(_keySpace, "KeySpace cannot be null. Use WithKeySpace to configure it.");
			ParameterNullException.ThrowIfNull(_serializer, "Serializer cannot be null. Use WithSerializer to configure it.");
			ParameterNullException.ThrowIfNull(_lifetimeProvider, "LifetimeProvider cannot be null. Use WithCustomLifetimeProvider to configure it.");
			ParameterNullException.ThrowIfNull(_uniqueKeyFactory, "UniqueKeyFactory cannot be null. Use WithUniqueKeyFactory to configure it.");
			ParameterNullException.ThrowIfNull(_oldConcurrencyTokenSelector, "OldConcurrencyTokenSelector cannot be null. Use WithOldConcurrencyTokenSelector to configure it.");
			ParameterNullException.ThrowIfNull(_newConcurrencyTokenSelector, "NewConcurrencyTokenSelector cannot be null. Use WithNewConcurrencyTokenSelector to configure it.");

			var logger = _loggerFactory.CreateLogger<ConcurrentEntityCollection<TKey, TEntity>>();

			var reader = new ResultReader(logger);

			var adapter = new HashsetAdapter(
				context: context,
				serializer: _serializer,
				reader: reader);

			return new ConcurrentEntityCollection<TKey, TEntity>(
				keySpace: _keySpace,
				adapter: adapter,
				lifetimeProvider: _lifetimeProvider,
				expirationUpdater: _expirationUpdater,
				context: context,
				uniqueKeyFactory: _uniqueKeyFactory,
				oldConcurrencyTokenSelector: _oldConcurrencyTokenSelector,
				newConcurrencyTokenSelector: _newConcurrencyTokenSelector);
		}
	}
}
