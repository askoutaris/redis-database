using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Collections;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using NSubstitute;
using StackExchange.Redis;
using System.Reflection;

namespace Tests.Collections;

public class EntityCollectionTests
{
	private readonly IStringAdapter _stringAdapter;
	private readonly ILifetimeProvider _lifetimeProvider;
	private readonly IExpirationUpdater _expirationUpdater;
	private readonly Func<int, string> _uniqueKeyFactory;
	private readonly EntityCollection<int, TestEntity> _collection;
	private const string _keySpace = "test:entities";

	public EntityCollectionTests()
	{
		_stringAdapter = Substitute.For<IStringAdapter>();
		_lifetimeProvider = Substitute.For<ILifetimeProvider>();
		_expirationUpdater = Substitute.For<IExpirationUpdater>();
		_uniqueKeyFactory = key => key.ToString();

		_collection = new EntityCollection<int, TestEntity>(
			_keySpace,
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueKeyFactory);
	}

	private static Task<T?> GetTask<T>(ReadResult<T> result)
	{
		var field = typeof(ReadResult<T>).GetField("_task", BindingFlags.NonPublic | BindingFlags.Instance);
		return (Task<T?>)field!.GetValue(result)!;
	}

	private class TestEntity
	{
		public string Name { get; set; } = string.Empty;
		public int Id { get; set; }
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var collection = new EntityCollection<int, TestEntity>(
			_keySpace,
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueKeyFactory);

		Assert.NotNull(collection);
	}
	#endregion

	#region TryGet Tests

	[Fact]
	public async Task TryGet_WithExistingEntity_ReturnsEntity()
	{
		// Arrange
		var key = 123;
		var expectedCacheKey = new RedisKey("test:entities|123");
		var expectedEntity = new TestEntity { Id = 123, Name = "Test Entity" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		await _stringAdapter.Received(1).TryGet<TestEntity>(expectedCacheKey);
		_lifetimeProvider.Received(1).GetCachingExpiration(key);
	}

	[Fact]
	public async Task TryGet_WithNonExistingEntity_ReturnsNull()
	{
		// Arrange
		var key = 456;
		var expectedCacheKey = new RedisKey("test:entities|456");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		Assert.Null(result);
		await _stringAdapter.Received(1).TryGet<TestEntity>(expectedCacheKey);
	}

	[Fact]
	public async Task TryGet_WithExtendExpirationOnReadsTrue_UpdatesLifetime()
	{
		// Arrange
		var key = 789;
		var expectedCacheKey = new RedisKey("test:entities|789");
		var expiration = TimeSpan.FromMinutes(10);
		var lifetime = new CachingLifetime(expiration, true);
		var expectedEntity = new TestEntity { Id = 789, Name = "Test" };

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedCacheKey, expiration);
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGet_WithExtendExpirationOnReadsFalse_DoesNotUpdateLifetime()
	{
		// Arrange
		var key = 321;
		var expectedCacheKey = new RedisKey("test:entities|321");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(15), false);
		var expectedEntity = new TestEntity { Id = 321, Name = "Test" };

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGet_CallsAdapterWithCorrectCacheKey()
	{
		// Arrange
		var key = 999;
		var expectedCacheKey = new RedisKey("test:entities|999");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await _collection.TryGet(key);

		// Assert
		await _stringAdapter.Received(1).TryGet<TestEntity>(expectedCacheKey);
	}

	[Fact]
	public async Task TryGet_WithStringKey_GeneratesCorrectCacheKey()
	{
		// Arrange
		var stringCollection = new EntityCollection<string, TestEntity>(
			"test:strings",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => key);

		var key = "test-string-key";
		var expectedCacheKey = new RedisKey("test:strings|test-string-key");
		var lifetime = new CachingLifetime(null, false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await stringCollection.TryGet(key);

		// Assert
		await _stringAdapter.Received(1).TryGet<TestEntity>(expectedCacheKey);
	}

	[Fact]
	public async Task TryGet_WhenAdapterThrows_PropagatesException()
	{
		// Arrange
		var key = 111;
		var expectedCacheKey = new RedisKey("test:entities|111");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedException = new InvalidOperationException("Redis error");

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns<Task<TestEntity?>>(_ => throw expectedException);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _collection.TryGet(key));
		Assert.Same(expectedException, exception);
	}

	[Fact]
	public async Task TryGet_WithCustomKeyFactory_UsesFactoryOutput()
	{
		// Arrange
		var customCollection = new EntityCollection<int, TestEntity>(
			"custom:space",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => $"custom_{key}");

		var key = 777;
		var expectedCacheKey = new RedisKey("custom:space|custom_777");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await customCollection.TryGet(key);

		// Assert
		await _stringAdapter.Received(1).TryGet<TestEntity>(expectedCacheKey);
	}

	[Fact]
	public async Task TryGet_MultipleCalls_CallsAdapterForEach()
	{
		// Arrange
		var keys = new[] { 1, 2, 3 };
		var entities = keys.Select(k => new TestEntity { Id = k, Name = $"Entity {k}" }).ToArray();
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		for (int i = 0; i < keys.Length; i++)
		{
			var cacheKey = new RedisKey($"test:entities|{keys[i]}");
			_stringAdapter.TryGet<TestEntity>(cacheKey).Returns(Task.FromResult<TestEntity?>(entities[i]));
		}

		// Act
		var results = new List<TestEntity?>();
		foreach (var key in keys)
		{
			results.Add(await _collection.TryGet(key));
		}

		// Assert
		for (int i = 0; i < keys.Length; i++)
		{
			var expectedKey = new RedisKey($"test:entities|{keys[i]}");
			await _stringAdapter.Received(1).TryGet<TestEntity>(expectedKey);
			Assert.Same(entities[i], results[i]);
		}
	}

	[Fact]
	public async Task TryGet_ExecutesImmediately_WithoutBatching()
	{
		// Arrange
		var key = 888;
		var expectedCacheKey = new RedisKey("test:entities|888");
		var expectedEntity = new TestEntity { Id = 888, Name = "Test" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryGet<TestEntity>(expectedCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		// Verify TryGet is called, not TryRead (which would be for batching)
		await _stringAdapter.Received(1).TryGet<TestEntity>(expectedCacheKey);
		_stringAdapter.DidNotReceive().TryRead<TestEntity>(Arg.Any<RedisKey>());
		Assert.Same(expectedEntity, result);
	}

	#endregion

	#region TryRead Tests

	[Fact]
	public void TryRead_WithValidKey_CallsAdapterWithCorrectCacheKey()
	{
		// Arrange
		var key = 123;
		var expectedCacheKey = new RedisKey("test:entities|123");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryRead<TestEntity>(expectedCacheKey).Returns(expectedResult);

		// Act
		var result = _collection.TryRead(key);

		// Assert
		_stringAdapter.Received(1).TryRead<TestEntity>(expectedCacheKey);
		_lifetimeProvider.Received(1).GetCachingExpiration(key);
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryRead_WithExtendExpirationOnReadsTrue_UpdatesLifetime()
	{
		// Arrange
		var key = 456;
		var expectedCacheKey = new RedisKey("test:entities|456");
		var expiration = TimeSpan.FromMinutes(10);
		var lifetime = new CachingLifetime(expiration, true);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryRead<TestEntity>(expectedCacheKey).Returns(expectedResult);

		// Act
		var result = _collection.TryRead(key);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedCacheKey, expiration);
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryRead_WithExtendExpirationOnReadsFalse_DoesNotUpdateLifetime()
	{
		// Arrange
		var key = 789;
		var expectedCacheKey = new RedisKey("test:entities|789");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(15), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryRead<TestEntity>(expectedCacheKey).Returns(expectedResult);

		// Act
		var result = _collection.TryRead(key);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryRead_WithStringKey_GeneratesCorrectCacheKey()
	{
		// Arrange
		var stringCollection = new EntityCollection<string, TestEntity>(
			"test:strings",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => key);

		var key = "test-string-key";
		var expectedCacheKey = new RedisKey("test:strings|test-string-key");
		var lifetime = new CachingLifetime(null, false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		stringCollection.TryRead(key);

		// Assert
		_stringAdapter.Received(1).TryRead<TestEntity>(expectedCacheKey);
	}

	#endregion

	#region Set Tests

	[Fact]
	public void Set_WithValidParameters_CallsAdapterWithCorrectParameters()
	{
		// Arrange
		var key = 100;
		var entity = new TestEntity { Id = 100, Name = "Test Entity" };
		var expectedCacheKey = new RedisKey("test:entities|100");
		var expiration = TimeSpan.FromHours(1);
		var lifetime = new CachingLifetime(expiration, false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.Set(key, entity);

		// Assert
		_stringAdapter.Received(1).Set(expectedCacheKey, entity, expiration);
		_lifetimeProvider.Received(1).GetCachingExpiration(key);
	}

	[Fact]
	public void Set_WithNullExpiration_CallsAdapterWithNullExpiration()
	{
		// Arrange
		var key = 200;
		var entity = new TestEntity { Id = 200, Name = "Test Entity 2" };
		var expectedCacheKey = new RedisKey("test:entities|200");
		var lifetime = new CachingLifetime(null, true);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.Set(key, entity);

		// Assert
		_stringAdapter.Received(1).Set(expectedCacheKey, entity, null);
	}

	[Fact]
	public void Set_WithComplexKey_GeneratesCorrectCacheKey()
	{
		// Arrange
		var complexCollection = new EntityCollection<(int, string), TestEntity>(
			"test:complex",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => $"{key.Item1}:{key.Item2}");

		var key = (42, "complex");
		var entity = new TestEntity { Id = 42, Name = "Complex Entity" };
		var expectedCacheKey = new RedisKey("test:complex|42:complex");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(30), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		complexCollection.Set(key, entity);

		// Assert
		_stringAdapter.Received(1).Set(expectedCacheKey, entity, TimeSpan.FromMinutes(30));
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_WithValidKey_CallsAdapterWithCorrectCacheKey()
	{
		// Arrange
		var key = 300;
		var expectedCacheKey = new RedisKey("test:entities|300");

		// Act
		_collection.Remove(key);

		// Assert
		_stringAdapter.Received(1).Remove(expectedCacheKey);
	}

	[Fact]
	public void Remove_WithStringKey_GeneratesCorrectCacheKey()
	{
		// Arrange
		var stringCollection = new EntityCollection<string, TestEntity>(
			"test:removals",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => key.ToUpperInvariant());

		var key = "remove-me";
		var expectedCacheKey = new RedisKey("test:removals|REMOVE-ME");

		// Act
		stringCollection.Remove(key);

		// Assert
		_stringAdapter.Received(1).Remove(expectedCacheKey);
	}

	#endregion

	#region Cache Key Generation Tests

	[Fact]
	public void GetCacheKey_WithSimpleKey_GeneratesCorrectFormat()
	{
		// This test verifies the cache key format indirectly through adapter calls
		// Arrange
		var key = 42;
		var lifetime = new CachingLifetime(null, false);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.TryRead(key);

		// Assert
		_stringAdapter.Received(1).TryRead<TestEntity>(new RedisKey("test:entities|42"));
	}

	[Fact]
	public void GetCacheKey_WithCustomKeyFactory_UsesFactoryOutput()
	{
		// Arrange
		var customCollection = new EntityCollection<int, TestEntity>(
			"custom:space",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => $"prefix_{key}_suffix");

		var key = 999;
		var lifetime = new CachingLifetime(null, false);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		customCollection.TryRead(key);

		// Assert
		_stringAdapter.Received(1).TryRead<TestEntity>(new RedisKey("custom:space|prefix_999_suffix"));
	}

	[Fact]
	public void GetCacheKey_WithSpecialCharacters_HandlesCorrectly()
	{
		// Arrange
		var specialCollection = new EntityCollection<string, TestEntity>(
			"special:chars",
			_stringAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			key => key);

		var key = "key:with|special*chars";
		var expectedCacheKey = new RedisKey("special:chars|key:with|special*chars");
		var lifetime = new CachingLifetime(null, false);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		specialCollection.TryRead(key);

		// Assert
		_stringAdapter.Received(1).TryRead<TestEntity>(expectedCacheKey);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void TryRead_Set_Remove_WorkflowIntegration()
	{
		// Arrange
		var key = 500;
		var entity = new TestEntity { Id = 500, Name = "Workflow Test" };
		var expectedCacheKey = new RedisKey("test:entities|500");
		var expiration = TimeSpan.FromMinutes(5);
		var lifetime = new CachingLifetime(expiration, true);
		var readResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(entity));

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_stringAdapter.TryRead<TestEntity>(expectedCacheKey).Returns(readResult);

		// Act & Assert - SetChild
		_collection.Set(key, entity);
		_stringAdapter.Received(1).Set(expectedCacheKey, entity, expiration);

		// Act & Assert - TryReadChild
		var result = _collection.TryRead(key);
		_stringAdapter.Received(1).TryRead<TestEntity>(expectedCacheKey);
		_expirationUpdater.Received(1).UpdateLifetime(expectedCacheKey, expiration);
		Assert.Same(GetTask(readResult), GetTask(result));

		// Act & Assert - RemoveChild
		_collection.Remove(key);
		_stringAdapter.Received(1).Remove(expectedCacheKey);
	}

	[Fact]
	public void MultipleOperations_WithDifferentKeys_GeneratesDifferentCacheKeys()
	{
		// Arrange
		var keys = new[] { 1, 2, 3 };
		var entities = keys.Select(k => new TestEntity { Id = k, Name = $"Entity {k}" }).ToArray();
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(1), false);

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);

		// Act
		for (int i = 0; i < keys.Length; i++)
		{
			_collection.Set(keys[i], entities[i]);
			_collection.TryRead(keys[i]);
			_collection.Remove(keys[i]);
		}

		// Assert
		for (int i = 0; i < keys.Length; i++)
		{
			var expectedKey = new RedisKey($"test:entities|{keys[i]}");
			_stringAdapter.Received(1).Set(expectedKey, entities[i], TimeSpan.FromMinutes(1));
			_stringAdapter.Received(1).TryRead<TestEntity>(expectedKey);
			_stringAdapter.Received(1).Remove(expectedKey);
		}
	}

	#endregion
}