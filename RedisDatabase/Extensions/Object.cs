using RedisDatabase.Logging;

namespace RedisDatabase.Extensions
{
	public static class ObjectExtensions
	{
		public static string ToJson(this object obj)
		{
			throw new NotImplementedException();
		}

		public static LogData<T> ToLogData<T>(this T? obj)
			=> new(obj);
	}
}
