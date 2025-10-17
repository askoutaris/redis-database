namespace Workbench.Scenarios.Shared
{
	public class Address
	{
		public int Id { get; }
		public string City { get; }

		public Address(int id, string city)
		{
			Id = id;
			City = city;
		}
	}
}
