using System.Reflection;
using RedisDatabase;

namespace Tests;

public class ReadResultTests
{
	private static Task<T?> GetTask<T>(ReadResult<T> result)
	{
		var field = typeof(ReadResult<T>).GetField("_task", BindingFlags.NonPublic | BindingFlags.Instance);
		return (Task<T?>)field!.GetValue(result)!;
	}

	[Fact]
	public void Constructor_WithTask_StoresTask()
	{
		var task = Task.FromResult<string?>("test");

		var result = new ReadResult<string>(task);

		Assert.Same(task, GetTask(result));
	}

	[Fact]
	public void Task_Property_ReturnsConstructorTask()
	{
		var task = Task.FromResult<string?>("test");
		var result = new ReadResult<string>(task);

		var retrievedTask = GetTask(result);

		Assert.Same(task, retrievedTask);
	}

	[Fact]
	public void Value_WhenTaskIsCompleted_ReturnsTaskResult()
	{
		var expectedValue = "test_value";
		var task = Task.FromResult<string?>(expectedValue);
		var result = new ReadResult<string>(task);

		var actualValue = result.Value;

		Assert.Equal(expectedValue, actualValue);
	}

	[Fact]
	public void Value_WhenTaskIsNotCompleted_ThrowsException()
	{
		var tcs = new TaskCompletionSource<string?>();
		var task = tcs.Task;
		var result = new ReadResult<string>(task);

		var exception = Assert.Throws<Exception>(() => result.Value);

		Assert.Equal("Read not performed yet - consider calling RedisContext.ExecuteBatch()", exception.Message);
	}

	[Fact]
	public void Value_WhenTaskIsCompletedWithNull_ReturnsNull()
	{
		var task = Task.FromResult<string?>(null);
		var result = new ReadResult<string>(task);

		var actualValue = result.Value;

		Assert.Null(actualValue);
	}

	[Fact]
	public void Value_WhenTaskIsCompletedWithFaulted_ThrowsOriginalException()
	{
		var expectedException = new InvalidOperationException("Test exception");
		var task = Task.FromException<string?>(expectedException);
		var result = new ReadResult<string>(task);

		var exception = Assert.Throws<AggregateException>(() => result.Value);

		Assert.Same(expectedException, exception.InnerException);
	}

	[Fact]
	public void Value_MultipleAccess_ReturnsSameValue()
	{
		var expectedValue = "consistent_value";
		var task = Task.FromResult<string?>(expectedValue);
		var result = new ReadResult<string>(task);

		var value1 = result.Value;
		var value2 = result.Value;

		Assert.Equal(expectedValue, value1);
		Assert.Equal(expectedValue, value2);
		Assert.Equal(value1, value2);
	}
}