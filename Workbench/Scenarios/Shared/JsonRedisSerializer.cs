using System.Text;
using Newtonsoft.Json;
using RedisDatabase.Serializers;

namespace Workbench.Scenarios.Shared
{
	public class JsonRedisSerializer : IRedisSerializer
	{
		private readonly JsonSerializerSettings _settings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Objects };

		public byte[] Serialize<TType>(TType value)
		{
			var json = JsonConvert.SerializeObject(value, _settings);
			var bytes = Encoding.UTF8.GetBytes(json);
			return bytes;
		}

		public TType? Deserialize<TType>(byte[] bytes)
		{
			var json = Encoding.UTF8.GetString(bytes);
			var entity = JsonConvert.DeserializeObject<TType>(json);
			return entity;
		}
	}
}
