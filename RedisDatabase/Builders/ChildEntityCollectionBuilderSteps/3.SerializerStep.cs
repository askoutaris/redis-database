using RedisDatabase.Serializers;

namespace RedisDatabase.Builders.ChildEntityCollectionBuilderSteps
{
	/// <summary>
	/// Third step in the child entity collection builder flow: configures the serializer.
	/// </summary>
	public interface ISerializerStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>Configures the serializer and advances to lifetime provider configuration.</summary>
		ILifetimeProviderStep<TParentKey, TChildKey, TEntity> WithSerializer(IRedisSerializer serializer);

		/// <summary>Configures the serializer with type signature validation to detect schema changes and invalidate incompatible cached data, then advances to lifetime provider configuration.</summary>
		ILifetimeProviderStep<TParentKey, TChildKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer);
	}

	class SerializerStep<TParentKey, TChildKey, TEntity> : ISerializerStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> _builder;

		public SerializerStep(IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
		{
			_builder = builder;
		}

		public ILifetimeProviderStep<TParentKey, TChildKey, TEntity> WithSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_builder.WithSerializer(serializer);

			return new LifetimeProviderStep<TParentKey, TChildKey, TEntity>(_builder);
		}

		public ILifetimeProviderStep<TParentKey, TChildKey, TEntity> WithSignaturedSerializer(IRedisSerializer serializer)
		{
			ArgumentNullException.ThrowIfNull(serializer, nameof(serializer));

			_builder.WithSignaturedSerializer(serializer);

			return new LifetimeProviderStep<TParentKey, TChildKey, TEntity>(_builder);
		}
	}
}
