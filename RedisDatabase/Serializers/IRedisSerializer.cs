namespace RedisDatabase.Serializers
{
	/// <summary>
	/// Provides binary serialization/deserialization for Redis storage, supporting various serialization formats.
	/// </summary>
	public interface IRedisSerializer
	{
		/// <summary>Deserializes a byte array to a typed object.</summary>
		/// <typeparam name="TType">The target type to deserialize to.</typeparam>
		/// <param name="bytes">The serialized byte array.</param>
		/// <returns>The deserialized object, or null if deserialization fails.</returns>
		TType? Deserialize<TType>(byte[] bytes);

		/// <summary>Serializes an object to a byte array.</summary>
		/// <typeparam name="TType">The type of object to serialize.</typeparam>
		/// <param name="value">The object to serialize.</param>
		/// <returns>The serialized byte array.</returns>
		byte[] Serialize<TType>(TType value);
	}
}
