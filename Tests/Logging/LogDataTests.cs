using RedisDatabase.Logging;

namespace Tests.Logging
{
	public class LogDataTests
	{
		private class TestData
		{
			public int Id { get; set; }
			public string Name { get; set; } = string.Empty;
			public DateTime CreatedAt { get; set; }
		}

		[Fact]
		public void Constructor_WithData_ShouldSetDataProperty()
		{
			// Arrange
			var testData = new TestData
			{
				Id = 1,
				Name = "Test",
				CreatedAt = new DateTime(2024, 1, 1)
			};

			// Act
			var logData = new LogData<TestData>(testData);

			// Assert
			Assert.Same(testData, logData.Data);
		}

		[Fact]
		public void Constructor_WithNullData_ShouldSetDataToNull()
		{
			// Act
			var logData = new LogData<TestData>(null);

			// Assert
			Assert.Null(logData.Data);
		}

		[Fact]
		public void Constructor_WithDataAndSerializerSettings_ShouldSetBothProperties()
		{
			// Arrange
			var testData = new TestData
			{
				Id = 2,
				Name = "Test with settings",
				CreatedAt = new DateTime(2024, 1, 15)
			};
			var settings = new System.Text.Json.JsonSerializerOptions
			{
				WriteIndented = false
			};

			// Act
			var logData = new LogData<TestData>(testData, settings);

			// Assert
			Assert.Same(testData, logData.Data);
		}

		[Fact]
		public void ToString_WithDataAndNoSettings_ShouldReturnJsonString()
		{
			// Arrange
			var testData = new TestData
			{
				Id = 3,
				Name = "Test JSON",
				CreatedAt = new DateTime(2024, 2, 1, 12, 30, 45)
			};
			var logData = new LogData<TestData>(testData);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.NotNull(result);
			Assert.NotEmpty(result);
			Assert.Contains("\"Id\":3", result);
			Assert.Contains("\"Name\":\"Test JSON\"", result);
			Assert.Contains("\"CreatedAt\"", result);
		}

		[Fact]
		public void ToString_WithDataAndCustomSettings_ShouldUseProvidedSettings()
		{
			// Arrange
			var testData = new TestData
			{
				Id = 4,
				Name = "Custom settings test",
				CreatedAt = new DateTime(2024, 3, 15, 10, 20, 30)
			};
			var settings = new System.Text.Json.JsonSerializerOptions
			{
				WriteIndented = false
			};
			var logData = new LogData<TestData>(testData, settings);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.NotNull(result);
			Assert.NotEmpty(result);
			Assert.Contains("\"Name\":\"Custom settings test\"", result);
		}

		[Fact]
		public void ToString_WithNullData_ShouldReturnNull()
		{
			// Arrange
			var logData = new LogData<TestData>(null);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.Equal("null", result);
		}

		[Fact]
		public void ToString_WithNullDataAndSettings_ShouldReturnNull()
		{
			// Arrange
			var settings = new System.Text.Json.JsonSerializerOptions();
			var logData = new LogData<TestData>(null, settings);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.Equal("null", result);
		}

		[Fact]
		public void LogData_WithPrimitiveType_ShouldWorkCorrectly()
		{
			// Arrange
			var logData = new LogData<int>(42);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.Equal("42", result);
			Assert.Equal(42, logData.Data);
		}

		[Fact]
		public void LogData_WithStringType_ShouldWorkCorrectly()
		{
			// Arrange
			var testString = "Test string value";
			var logData = new LogData<string>(testString);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.Equal("\"Test string value\"", result);
			Assert.Equal(testString, logData.Data);
		}

		[Fact]
		public void LogData_WithCollectionType_ShouldSerializeCorrectly()
		{
			// Arrange
			var testList = new List<int> { 1, 2, 3, 4, 5 };
			var logData = new LogData<List<int>>(testList);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.Equal("[1,2,3,4,5]", result);
			Assert.Equal(testList, logData.Data);
		}

		[Fact]
		public void LogData_WithDictionaryType_ShouldSerializeCorrectly()
		{
			// Arrange
			var testDictionary = new Dictionary<string, int>
			{
				{ "one", 1 },
				{ "two", 2 },
				{ "three", 3 }
			};
			var logData = new LogData<Dictionary<string, int>>(testDictionary);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.NotNull(result);
			Assert.NotEmpty(result);
			Assert.Contains("\"one\":1", result);
			Assert.Contains("\"two\":2", result);
			Assert.Contains("\"three\":3", result);
			Assert.Equal(testDictionary, logData.Data);
		}

		[Fact]
		public void LogData_WithNestedObjectType_ShouldSerializeCorrectly()
		{
			// Arrange
			var nestedData = new
			{
				Level1 = new
				{
					Level2 = new
					{
						Value = "Nested value"
					}
				}
			};
			var logData = new LogData<object>(nestedData);

			// Act
			var result = logData.ToString();

			// Assert
			Assert.NotNull(result);
			Assert.NotEmpty(result);
			Assert.Contains("Nested value", result);
		}
	}
}