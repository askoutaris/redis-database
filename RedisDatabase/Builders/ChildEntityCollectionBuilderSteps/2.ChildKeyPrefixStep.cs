namespace RedisDatabase.Builders.ChildEntityCollectionBuilderSteps
{
	/// <summary>
	/// Second step in the child entity collection builder flow: configures the hash field prefix for child entities within the parent hash.
	/// This prefix is used to form the hash field name that stores each child entity.
	/// </summary>
	public interface IChildKeyPrefixStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		/// <summary>
		/// Configures the hash field prefix for child entities.
		/// Example: For childKeyPrefix "item" and child key "5", the hash field will be "item:5".
		/// </summary>
		/// <param name="childKeyPrefix">The prefix for child entity hash fields.</param>
		ISerializerStep<TParentKey, TChildKey, TEntity> WithChildKeyPrefix(string childKeyPrefix);
	}

	class ChildKeyPrefixStep<TParentKey, TChildKey, TEntity> : IChildKeyPrefixStep<TParentKey, TChildKey, TEntity>
		where TParentKey : notnull
		where TEntity : class
	{
		private readonly IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> _builder;

		public ChildKeyPrefixStep(IChildEntityCollectionBuilder<TParentKey, TChildKey, TEntity> builder)
		{
			_builder = builder;
		}

		public ISerializerStep<TParentKey, TChildKey, TEntity> WithChildKeyPrefix(string childKeyPrefix)
		{
			ArgumentNullException.ThrowIfNull(childKeyPrefix, nameof(childKeyPrefix));

			_builder.WithChildKeyPrefix(childKeyPrefix);

			return new SerializerStep<TParentKey, TChildKey, TEntity>(_builder);
		}
	}
}
