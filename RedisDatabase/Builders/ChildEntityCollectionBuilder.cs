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
	/// Fluent builder for configuring <see cref="IChildEntityCollection{TParentKey, TChildKey, TEntity}"/> instances with Redis hash storage.
	/// </summary>
	/// <typeparam name="TParentKey">The parent entity key type.</typeparam>
	/// <typeparam name="TChildKey">The child entity key type.</typeparam>
	/// <typeparam name="TEntity">The child entity type to cache.</typeparam>
	public interface IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> : ICollectionBuilder
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>Configures the Redis hash key prefix for the parent collection.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithKeySpace(string keySpace);

		/// <summary>Configures the hash field prefix for child entities within the parent hash.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithChildKeyPrefix(string childKeyPrefix);

		/// <summary>Configures the serializer for entity serialization/deserialization.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithSerializer(IRedisSerializer serializer);

		/// <summary>Configures the serializer with type signature validation to detect schema changes and invalidate incompatible cached data.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer);

		/// <summary>Configures a custom lifetime provider for key expiration management.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider lifetimeProvider);

		/// <summary>Configures a default lifetime provider with fixed expiration time and optional read extension.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false);

		/// <summary>Configures the collection to have no expiration (keys persist indefinitely).</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithNoExpiration();

		/// <summary>Configures the expiration updater strategy for key lifetime management.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithExpirationUpdater(IExpirationUpdater expirationUpdater);

		/// <summary>Configures the factory function to convert parent keys to unique Redis hash key strings.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithUniqueParentKeyFactory(Func<TParentKey, string> uniqueParentKeyFactory);

		/// <summary>Configures the factory function to convert child keys to unique hash field name strings.</summary>
		IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithUniqueChildKeyFactory(Func<TChildKey, string> uniqueChildKeyFactory);

		/// <summary>Builds the child entity collection instance with the specified Redis context.</summary>
		internal IChildEntityCollection<TParentKey, TChildKey, TEntity> Build(IRedisContext context);
	}

	class ChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> : IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private string? _keySpace;
		private string? _childKeyPrefix;
		private IRedisSerializer? _serializer;
		private ILifetimeProvider? _lifetimeProvider;
		private IExpirationUpdater _expirationUpdater;
		private readonly ILoggerFactory _loggerFactory;
		private Func<TParentKey, string>? _uniqueParentKeyFactory;
		private Func<TChildKey, string>? _uniqueChildKeyFactory;

		public ChildEntityCollectionBuilder(IExpirationUpdater defaultExpirationUpdater, ILoggerFactory loggerFactory)
		{
			_expirationUpdater = defaultExpirationUpdater;
			_loggerFactory = loggerFactory;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithKeySpace(string keySpace)
		{
			ArgumentNullException.ThrowIfNull(keySpace, nameof(keySpace));

			_keySpace = keySpace;

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithChildKeyPrefix(string childKeyPrefix)
		{
			ArgumentNullException.ThrowIfNull(childKeyPrefix, nameof(childKeyPrefix));

			_childKeyPrefix = childKeyPrefix;

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_serializer = serializer;

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer)
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

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithCustomLifetimeProvider(ILifetimeProvider lifetimeProvider)
		{
			ArgumentNullException.ThrowIfNull(lifetimeProvider, nameof(lifetimeProvider));

			_lifetimeProvider = lifetimeProvider;

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithDefaultLifetimeProvider(TimeSpan expiration, bool extendExpirationOnReads = false)
		{
			if (expiration <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero");

			_lifetimeProvider = new DefaultLifetimeProvider(expiration, extendExpirationOnReads);

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithNoExpiration()
		{
			_lifetimeProvider = new DefaultLifetimeProvider(null, false);

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithExpirationUpdater(IExpirationUpdater expirationUpdater)
		{
			ArgumentNullException.ThrowIfNull(expirationUpdater, nameof(expirationUpdater));

			_expirationUpdater = expirationUpdater;

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithUniqueParentKeyFactory(Func<TParentKey, string> uniqueParentKeyFactory)
		{
			ArgumentNullException.ThrowIfNull(uniqueParentKeyFactory, nameof(uniqueParentKeyFactory));

			_uniqueParentKeyFactory = uniqueParentKeyFactory;

			return this;
		}

		public IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> WithUniqueChildKeyFactory(Func<TChildKey, string> uniqueChildKeyFactory)
		{
			ArgumentNullException.ThrowIfNull(uniqueChildKeyFactory, nameof(uniqueChildKeyFactory));

			_uniqueChildKeyFactory = uniqueChildKeyFactory;

			return this;
		}

		public IChildEntityCollection<TParentKey, TChildKey, TEntity> Build(IRedisContext context)
		{
			ArgumentNullException.ThrowIfNull(context, nameof(context));

			ParameterNullException.ThrowIfNull(_keySpace, "KeySpace cannot be null. Use WithKeySpace to configure it.");
			ParameterNullException.ThrowIfNull(_childKeyPrefix, "ChildKeyPrefix cannot be null. Use WithChildKeyPrefix to configure it.");
			ParameterNullException.ThrowIfNull(_serializer, "Serializer cannot be null. Use WithSerializer to configure it.");
			ParameterNullException.ThrowIfNull(_lifetimeProvider, "LifetimeProvider cannot be null. Use WithCustomLifetimeProvider to configure it.");
			ParameterNullException.ThrowIfNull(_uniqueParentKeyFactory, "UniqueParentKeyFactory cannot be null. Use WithUniqueParentKeyFactory to configure it.");
			ParameterNullException.ThrowIfNull(_uniqueChildKeyFactory, "UniqueChildKeyFactory cannot be null. Use WithUniqueChildKeyFactory to configure it.");

			var logger = _loggerFactory.CreateLogger<ChildEntityCollection<TParentKey, TChildKey, TEntity>>();

			var reader = new ResultReader(logger);

			var adapter = new HashsetAdapter(
				context: context,
				serializer: _serializer,
				reader: reader);

			return new ChildEntityCollection<TParentKey, TChildKey, TEntity>(
				keySpace: _keySpace,
				childKeyPrefix: _childKeyPrefix,
				adapter: adapter,
				lifetimeProvider: _lifetimeProvider,
				expirationUpdater: _expirationUpdater,
				uniqueHashsetKeyFactory: _uniqueParentKeyFactory,
				uniqueFieldKeyFactory: _uniqueChildKeyFactory);
		}
	}
}
