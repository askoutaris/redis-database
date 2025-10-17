using RedisDatabase.Serializers;

namespace RedisDatabase.Builders.EntityCollectionBuilderSteps
{
	/// <summary>
	/// Second step in the entity collection builder flow: configures the serializer.
	/// </summary>
	public interface ISerializerStep<TKey, TEntity>
		where TKey : notnull
		where TEntity : class
	{
		/// <summary>Configures the serializer and advances to lifetime provider configuration.</summary>
		ILifetimeProviderStep<TKey, TEntity> WithSerializer(IRedisSerializer serializer);

		/// <summary>Configures the serializer with type signature validation to detect schema changes and invalidate incompatible cached data, then advances to lifetime provider configuration.</summary>
		ILifetimeProviderStep<TKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer);
	}

	class SerializerStep<TKey, TEntity> : ISerializerStep<TKey, TEntity> where TKey : notnull
			where TEntity : class
	{
		private readonly IEntityCollectionBuilder<TKey, TEntity> _builder;

		public SerializerStep(IEntityCollectionBuilder<TKey, TEntity> builder)
		{
			_builder = builder;
		}

		public ILifetimeProviderStep<TKey, TEntity> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_builder.WithSerializer(serializer);

			return new LifetimeProviderStep<TKey, TEntity>(_builder);
		}

		public ILifetimeProviderStep<TKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_builder.WithSignaturedSerializer(serializer);

			return new LifetimeProviderStep<TKey, TEntity>(_builder);
		}
	}
}
