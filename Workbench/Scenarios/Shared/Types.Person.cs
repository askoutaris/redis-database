namespace Workbench.Scenarios.Shared
{
	public class Person
	{
		public int Id { get; }
		public long Version { get; set; }
		public string Name { get; set; }

		public Person(int id, long version, string name)
		{
			Id = id;
			Version = version;
			Name = name;
		}

		public long GetNextVersion()
		{
			return ++Version;
		}
	}
}
