using RedisDatabase.Extensions;
using RedisDatabase.Logging;

namespace Tests.Extensions
{
	public class ObjectExtensionsTests
	{
		[Fact]
		public void ToLogData_WithNonNullReferenceType_ReturnsLogDataWithData()
		{
			// Arrange
			var testString = "test value";

			// Act
			var result = testString.ToLogData();

			// Assert
			Assert.Equal(testString, result.Data);
		}

		[Fact]
		public void ToLogData_WithNullReferenceType_ReturnsLogDataWithNullData()
		{
			// Arrange
			string? testString = null;

			// Act
			var result = testString.ToLogData();

			// Assert
			Assert.Null(result.Data);
		}

		[Fact]
		public void ToLogData_WithValueType_ReturnsLogDataWithData()
		{
			// Arrange
			var testInt = 42;

			// Act
			var result = testInt.ToLogData();

			// Assert
			Assert.Equal(testInt, result.Data);
		}

		[Fact]
		public void ToLogData_WithNullableValueType_ReturnsLogDataWithData()
		{
			// Arrange
			int? testInt = 42;

			// Act
			var result = testInt.ToLogData();

			// Assert
			Assert.Equal(testInt, result.Data);
		}

		[Fact]
		public void ToLogData_WithNullNullableValueType_ReturnsLogDataWithNullData()
		{
			// Arrange
			int? testInt = null;

			// Act
			var result = testInt.ToLogData();

			// Assert
			Assert.Null(result.Data);
		}

		[Fact]
		public void ToLogData_WithComplexObject_ReturnsLogDataWithData()
		{
			// Arrange
			var testObject = new TestClass { Id = 1, Name = "Test" };

			// Act
			var result = testObject.ToLogData();

			// Assert
			Assert.NotNull(result.Data);
			Assert.Equal(testObject.Id, result.Data.Id);
			Assert.Equal(testObject.Name, result.Data.Name);
		}

		[Fact]
		public void ToLogData_ReturnsLogDataType()
		{
			// Arrange
			var testString = "test";

			// Act
			var result = testString.ToLogData();

			// Assert
			Assert.IsType<LogData<string>>(result);
		}

		[Fact]
		public void ToLogData_WithDifferentTypes_PreservesGenericType()
		{
			// Arrange
			var testString = "test";
			var testInt = 42;
			var testBool = true;

			// Act
			var stringResult = testString.ToLogData();
			var intResult = testInt.ToLogData();
			var boolResult = testBool.ToLogData();

			// Assert
			Assert.IsType<LogData<string>>(stringResult);
			Assert.IsType<LogData<int>>(intResult);
			Assert.IsType<LogData<bool>>(boolResult);
		}

		[Fact]
		public void ToLogData_CanBeSerializedToString()
		{
			// Arrange
			var testString = "test value";

			// Act
			var result = testString.ToLogData();
			var serialized = result.ToString();

			// Assert
			Assert.NotNull(serialized);
			Assert.Contains("test value", serialized);
		}

		[Fact]
		public void ToLogData_WithComplexObject_CanBeSerializedToString()
		{
			// Arrange
			var testObject = new TestClass { Id = 1, Name = "Test" };

			// Act
			var result = testObject.ToLogData();
			var serialized = result.ToString();

			// Assert
			Assert.NotNull(serialized);
			Assert.Contains("1", serialized);
			Assert.Contains("Test", serialized);
		}

		private class TestClass
		{
			public int Id { get; set; }
			public string? Name { get; set; }
		}
	}
}