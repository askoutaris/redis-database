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
	/// Fluent builder for configuring <see cref="IEntityCollection{TKey, TEntity}"/> instances with Redis string storage.
	/// </summary>
	/// <typeparam name="TKey">The entity key type.</typeparam>
	/// <typeparam name="TEntity">The entity type to cache.</typeparam>
	public interface IEntityCollectionBuilder<TKey, TEntity> : ICollectionBuilder
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>Configures the Redis key prefix for the collection.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithKeySpace(string keySpace);

		/// <summary>Configures the serializer for entity serialization/deserialization.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithSerializer(IRedisSerializer serializer);

		/// <summary>Configures the serializer with type signature validation to detect schema changes and invalidate incompatible cached data.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer);

		/// <summary>Configures a custom lifetime provider for key expiration management.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider lifetimeProvider);

		/// <summary>Configures a default lifetime provider with fixed expiration time and optional read extension.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false);

		/// <summary>Configures the collection to have no expiration (keys persist indefinitely).</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithNoExpiration();

		/// <summary>Configures the expiration updater strategy for key lifetime management.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithExpirationUpdater(IExpirationUpdater expirationUpdater);

		/// <summary>Configures the factory function to convert entity keys to unique Redis key strings.</summary>
		IEntityCollectionBuilder<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> uniqueKeyFactory);

		/// <summary>Builds the entity collection instance with the specified Redis context.</summary>
		internal IEntityCollection<TKey, TEntity> Build(IRedisContext context);
	}

	class EntityCollectionBuilder<TKey, TEntity> : IEntityCollectionBuilder<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		private string? _keySpace;
		private IRedisSerializer? _serializer;
		private ILifetimeProvider? _lifetimeProvider;
		private IExpirationUpdater _expirationUpdater;
		private readonly ILoggerFactory _loggerFactory;
		private Func<TKey, string>? _uniqueKeyFactory;

		public EntityCollectionBuilder(IExpirationUpdater defaultExpirationUpdater, ILoggerFactory loggerFactory)
		{
			_expirationUpdater = defaultExpirationUpdater;
			_loggerFactory = loggerFactory;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithKeySpace(string keySpace)
		{
			ArgumentNullException.ThrowIfNull(keySpace, nameof(keySpace));

			_keySpace = keySpace;

			return this;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_serializer = serializer;

			return this;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer)
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

		public IEntityCollectionBuilder<TKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider lifetimeProvider)
		{
			ArgumentNullException.ThrowIfNull(lifetimeProvider, nameof(lifetimeProvider));

			_lifetimeProvider = lifetimeProvider;

			return this;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false)
		{
			if (expiration <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero");

			_lifetimeProvider = new DefaultLifetimeProvider(expiration, extendExpirationOnReads);

			return this;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithNoExpiration()
		{
			_lifetimeProvider = new DefaultLifetimeProvider(null, false);

			return this;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithExpirationUpdater(IExpirationUpdater expirationUpdater)
		{
			ArgumentNullException.ThrowIfNull(expirationUpdater, nameof(expirationUpdater));

			_expirationUpdater = expirationUpdater;

			return this;
		}

		public IEntityCollectionBuilder<TKey, TEntity> WithUniqueKeyFactory(Func<TKey, string> uniqueKeyFactory)
		{
			ArgumentNullException.ThrowIfNull(uniqueKeyFactory, nameof(uniqueKeyFactory));

			_uniqueKeyFactory = uniqueKeyFactory;

			return this;
		}

		public IEntityCollection<TKey, TEntity> Build(IRedisContext context)
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			ParameterNullException.ThrowIfNull(_keySpace, "KeySpace cannot be null. Use WithKeySpace to configure it.");
			ParameterNullException.ThrowIfNull(_serializer, "Serializer cannot be null. Use WithSerializer to configure it.");
			ParameterNullException.ThrowIfNull(_lifetimeProvider, "LifetimeProvider cannot be null. Use WithCustomLifetimeProvider to configure it.");
			ParameterNullException.ThrowIfNull(_expirationUpdater, "ExpirationUpdater cannot be null. Use WithExpirationUpdaterto configure it.");
			ParameterNullException.ThrowIfNull(_uniqueKeyFactory, "UniqueKeyFactory cannot be null. Use WithUniqueKeyFactory to configure it.");

			var logger = _loggerFactory.CreateLogger<EntityCollection<TKey, TEntity>>();

			var reader = new ResultReader(logger);

			var adapter = new StringAdapter(
				context: context,
				serializer: _serializer,
				reader: reader);

			return new EntityCollection<TKey, TEntity>(
				keySpace: _keySpace,
				adapter: adapter,
				lifetimeProvider: _lifetimeProvider,
				expirationUpdater: _expirationUpdater,
				uniqueKeyFactory: _uniqueKeyFactory);
		}
	}
}
