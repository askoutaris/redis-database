using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Collections;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using NSubstitute;
using StackExchange.Redis;
using System.Reflection;

namespace Tests.Collections;

public class ConcurrentEntityCollectionTests
{
	private readonly IHashsetAdapter _hashsetAdapter;
	private readonly ILifetimeProvider _lifetimeProvider;
	private readonly IExpirationUpdater _expirationUpdater;
	private readonly IRedisContext _redisContext;
	private readonly Func<int, string> _uniqueKeyFactory;
	private readonly Func<TestEntity, string?> _oldConcurrencyTokenSelector;
	private readonly Func<TestEntity, string> _newConcurrencyTokenSelector;
	private readonly ConcurrentEntityCollection<int, TestEntity> _collection;
	private const string _keySpace = "concurrent:entities";

	public ConcurrentEntityCollectionTests()
	{
		_hashsetAdapter = Substitute.For<IHashsetAdapter>();
		_lifetimeProvider = Substitute.For<ILifetimeProvider>();
		_expirationUpdater = Substitute.For<IExpirationUpdater>();
		_redisContext = Substitute.For<IRedisContext>();
		_uniqueKeyFactory = key => key.ToString();
		_oldConcurrencyTokenSelector = entity => entity.OldVersion;
		_newConcurrencyTokenSelector = entity => entity.NewVersion;

		_collection = new ConcurrentEntityCollection<int, TestEntity>(
			_keySpace,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_redisContext,
			_uniqueKeyFactory,
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);
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
		public string? OldVersion { get; set; }
		public string NewVersion { get; set; } = string.Empty;
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		var collection = new ConcurrentEntityCollection<int, TestEntity>(
			_keySpace,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_redisContext,
			_uniqueKeyFactory,
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);

		Assert.NotNull(collection);
	}

	[Fact]
	public void Constructor_WithNullLifetimeProvider_DoesNotThrow()
	{
		// lifetimeProvider is nullable and doesn't throw
		var collection = new ConcurrentEntityCollection<int, TestEntity>(
			_keySpace,
			_hashsetAdapter,
			null!,
			_expirationUpdater,
			_redisContext,
			_uniqueKeyFactory,
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);

		Assert.NotNull(collection);
	}

	#endregion

	#region TryGet Tests

	[Fact]
	public async Task TryGet_WithExistingEntity_ReturnsEntity()
	{
		// Arrange
		var key = 123;
		var expectedHashsetKey = new RedisKey("concurrent:entities|123");
		var expectedField = new RedisValue("obj");
		var expectedEntity = new TestEntity { Id = 123, Name = "Test Entity", NewVersion = "v1" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(expectedEntity));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
	}

	[Fact]
	public async Task TryGet_WithNonExistingEntity_ReturnsNull()
	{
		// Arrange
		var key = 456;
		var expectedHashsetKey = new RedisKey("concurrent:entities|456");
		var expectedField = new RedisValue("obj");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(null));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		Assert.Null(result);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
	}

	[Fact]
	public async Task TryGet_CallsTouch_UpdatesExpirationWhenExtendOnReads()
	{
		// Arrange
		var key = 789;
		var expectedHashsetKey = new RedisKey("concurrent:entities|789");
		var expectedField = new RedisValue("obj");
		var expiration = TimeSpan.FromMinutes(10);
		var lifetime = new CachingLifetime(expiration, true);
		var expectedEntity = new TestEntity { Id = 789, Name = "Test", NewVersion = "v1" };

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(expectedEntity));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedHashsetKey, expiration);
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGet_CallsTouch_DoesNotUpdateExpirationWhenNoExtendOnReads()
	{
		// Arrange
		var key = 321;
		var expectedHashsetKey = new RedisKey("concurrent:entities|321");
		var expectedField = new RedisValue("obj");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(15), false);
		var expectedEntity = new TestEntity { Id = 321, Name = "Test", NewVersion = "v1" };

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(expectedEntity));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGet_CallsAdapterWithCorrectHashsetKeyAndField()
	{
		// Arrange
		var key = 999;
		var expectedHashsetKey = new RedisKey("concurrent:entities|999");
		var expectedField = new RedisValue("obj");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(null));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		await _collection.TryGet(key);

		// Assert
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
	}

	[Fact]
	public async Task TryGet_WithStringKey_GeneratesCorrectHashsetKey()
	{
		// Arrange
		var stringCollection = new ConcurrentEntityCollection<string, TestEntity>(
			"string:entities",
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_redisContext,
			key => key.ToUpperInvariant(),
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);

		var key = "test-key";
		var expectedHashsetKey = new RedisKey("string:entities|TEST-KEY");
		var expectedField = new RedisValue("obj");
		var lifetime = new CachingLifetime(null, false);

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(null));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		await stringCollection.TryGet(key);

		// Assert
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
	}

	[Fact]
	public async Task TryGet_WhenAdapterThrows_PropagatesException()
	{
		// Arrange
		var key = 111;
		var expectedHashsetKey = new RedisKey("concurrent:entities|111");
		var expectedField = new RedisValue("obj");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedException = new InvalidOperationException("Redis error");

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns<Task<TestEntity?>>(_ => throw expectedException);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _collection.TryGet(key));
		Assert.Same(expectedException, exception);
	}

	[Fact]
	public async Task TryGet_WithCustomKeyFactory_UsesFactoryOutput()
	{
		// Arrange
		var customCollection = new ConcurrentEntityCollection<int, TestEntity>(
			"custom:space",
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_redisContext,
			key => $"custom_{key}",
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);

		var key = 777;
		var expectedHashsetKey = new RedisKey("custom:space|custom_777");
		var expectedField = new RedisValue("obj");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(null));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		await customCollection.TryGet(key);

		// Assert
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
	}

	[Fact]
	public async Task TryGet_MultipleCalls_CallsAdapterForEach()
	{
		// Arrange
		var keys = new[] { 1, 2, 3 };
		var entities = keys.Select(k => new TestEntity { Id = k, Name = $"Entity {k}", NewVersion = $"v{k}" }).ToArray();
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedField = new RedisValue("obj");

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		for (int i = 0; i < keys.Length; i++)
		{
			var hashsetKey = new RedisKey($"concurrent:entities|{keys[i]}");
			_hashsetAdapter.TryGetField<TestEntity>(hashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(entities[i]));
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
			var expectedHashsetKey = new RedisKey($"concurrent:entities|{keys[i]}");
			await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
			Assert.Same(entities[i], results[i]);
		}
	}

	[Fact]
	public async Task TryGet_ExecutesImmediately_WithoutBatching()
	{
		// Arrange
		var key = 888;
		var expectedHashsetKey = new RedisKey("concurrent:entities|888");
		var expectedField = new RedisValue("obj");
		var expectedEntity = new TestEntity { Id = 888, Name = "Test", NewVersion = "v1" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_hashsetAdapter.TryGetField<TestEntity>(expectedHashsetKey, expectedField).Returns(Task.FromResult<TestEntity?>(expectedEntity));
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		var result = await _collection.TryGet(key);

		// Assert
		// Verify TryGetField is called, not TryReadField (which would be for batching)
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedHashsetKey, expectedField);
		_hashsetAdapter.DidNotReceive().TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>());
		Assert.Same(expectedEntity, result);
	}

	#endregion

	#region TryRead Tests

	[Fact]
	public void TryRead_WithValidKey_CallsAdapterWithCorrectParameters()
	{
		// Arrange
		var key = 123;
		var expectedHashsetKey = new RedisKey("concurrent:entities|123");
		var expectedField = new RedisValue("obj");
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_hashsetAdapter.TryReadField<TestEntity>(expectedHashsetKey, expectedField).Returns(expectedResult);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		var result = _collection.TryRead(key);

		// Assert
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedHashsetKey, expectedField);
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryRead_CallsTouch_UpdatesExpirationWhenExtendOnReads()
	{
		// Arrange
		var key = 456;
		var expectedHashsetKey = new RedisKey("concurrent:entities|456");
		var expiration = TimeSpan.FromMinutes(10);
		var lifetime = new CachingLifetime(expiration, true);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_hashsetAdapter.TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(expectedResult);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.TryRead(key);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedHashsetKey, expiration);
	}

	[Fact]
	public void TryRead_CallsTouch_DoesNotUpdateExpirationWhenNoExtendOnReads()
	{
		// Arrange
		var key = 789;
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(15), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_hashsetAdapter.TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(expectedResult);
		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.TryRead(key);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
	}
	#endregion

	#region Set Tests

	[Fact]
	public void Set_WithValidParameters_CallsAdapterAndSetsExpiration()
	{
		// Arrange
		var key = 100;
		var entity = new TestEntity { Id = 100, Name = "Test Entity", NewVersion = "v1" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|100");
		var expectedEntityField = new RedisValue("obj");
		var expiration = TimeSpan.FromHours(1);
		var lifetime = new CachingLifetime(expiration, false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.Set(key, entity);

		// Assert
		_hashsetAdapter.Received(1).SetField(expectedHashsetKey, expectedEntityField, entity);
		_redisContext.Received(2).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>()); // One for expiration, one for version
		_lifetimeProvider.Received(1).GetCachingExpiration(key);
	}

	[Fact]
	public void Set_CallsKeyExpireAsync_WithCorrectParameters()
	{
		// Arrange
		var key = 200;
		var entity = new TestEntity { Id = 200, Name = "Test Entity 2", NewVersion = "v2" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|200");
		var expiration = TimeSpan.FromMinutes(30);
		var lifetime = new CachingLifetime(expiration, false);
		var database = Substitute.For<IDatabaseAsync>();

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		Func<IDatabaseAsync, Task>? capturedExpirationCommand = null;
		var callCount = 0;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action =>
		{
			callCount++;
			if (callCount == 2) // Second call is for expiration (first is for version)
				capturedExpirationCommand = action;
		}));

		// Act
		_collection.Set(key, entity);

		// Assert
		Assert.NotNull(capturedExpirationCommand);
		capturedExpirationCommand(database);
		database.Received(1).KeyExpireAsync(expectedHashsetKey, expiration, Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>());
	}

	[Fact]
	public void Set_WithNewEntity_SetsKeyNotExistsCondition()
	{
		// Arrange
		var key = 300;
		var entity = new TestEntity { Id = 300, Name = "New Entity", OldVersion = null, NewVersion = "v1" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|300");
		var expectedVersionField = new RedisValue("ct");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var database = Substitute.For<IDatabaseAsync>();

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		Func<IDatabaseAsync, Task>? capturedVersionCommand = null;
		var callCount = 0;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action =>
		{
			callCount++;
			if (callCount == 1) // First call is for version (SetVersion is called first)
				capturedVersionCommand = action;
		}));

		// Act
		_collection.Set(key, entity);

		// Assert
		_redisContext.Received(1).AddCondition(Arg.Any<Condition>());
		Assert.NotNull(capturedVersionCommand);
		capturedVersionCommand(database);
		database.Received(1).HashSetAsync(expectedHashsetKey, expectedVersionField, "v1");
	}

	[Fact]
	public void Set_WithExistingEntity_SetsHashEqualCondition()
	{
		// Arrange
		var key = 400;
		var entity = new TestEntity { Id = 400, Name = "Existing Entity", OldVersion = "v1", NewVersion = "v2" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|400");
		var expectedVersionField = new RedisValue("ct");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var database = Substitute.For<IDatabaseAsync>();

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		Func<IDatabaseAsync, Task>? capturedVersionCommand = null;
		var callCount = 0;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action =>
		{
			callCount++;
			if (callCount == 1) // First call is for version (SetVersion is called first)
				capturedVersionCommand = action;
		}));

		// Act
		_collection.Set(key, entity);

		// Assert
		_redisContext.Received(1).AddCondition(Arg.Any<Condition>());
		Assert.NotNull(capturedVersionCommand);
		capturedVersionCommand(database);
		database.Received(1).HashSetAsync(expectedHashsetKey, expectedVersionField, "v2");
	}

	[Fact]
	public void Set_MultipleCalls_CallsSetFieldEachTime()
	{
		// Arrange
		var key = 500;
		var entity1 = new TestEntity { Id = 500, Name = "Entity 1", NewVersion = "v1" };
		var entity2 = new TestEntity { Id = 500, Name = "Entity 2", NewVersion = "v2" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.Set(key, entity1);
		_collection.Set(key, entity2);
		_collection.Set(key, entity1);

		// Assert - no deduplication, each call goes through
		_hashsetAdapter.Received(3).SetField(Arg.Any<RedisKey>(), new RedisValue("obj"), Arg.Any<TestEntity>());
		_redisContext.Received(6).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>()); // 3 calls × (1 expiration + 1 version)
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_WithValidKey_CallsAdapterWithCorrectHashsetKey()
	{
		// Arrange
		var key = 600;
		var expectedHashsetKey = new RedisKey("concurrent:entities|600");

		// Act
		_collection.Remove(key);

		// Assert
		_hashsetAdapter.Received(1).Remove(expectedHashsetKey);
	}

	[Fact]
	public void Remove_WithStringKey_GeneratesCorrectHashsetKey()
	{
		// Arrange
		var stringCollection = new ConcurrentEntityCollection<string, TestEntity>(
			"string:entities",
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_redisContext,
			key => key.ToUpperInvariant(),
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);

		var key = "test-key";
		var expectedHashsetKey = new RedisKey("string:entities|TEST-KEY");

		// Act
		stringCollection.Remove(key);

		// Assert
		_hashsetAdapter.Received(1).Remove(expectedHashsetKey);
	}

	#endregion

	#region Touch Tests

	[Fact]
	public void Touch_WithExtendExpirationOnReadsTrue_UpdatesLifetime()
	{
		// Arrange
		var key = 700;
		var expectedHashsetKey = new RedisKey("concurrent:entities|700");
		var expiration = TimeSpan.FromMinutes(20);
		var lifetime = new CachingLifetime(expiration, true);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.Touch(key);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedHashsetKey, expiration);
		_lifetimeProvider.Received(1).GetCachingExpiration(key);
	}

	[Fact]
	public void Touch_WithExtendExpirationOnReadsFalse_DoesNotUpdateLifetime()
	{
		// Arrange
		var key = 800;
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(25), false);

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		// Act
		_collection.Touch(key);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
	}
	#endregion

	#region Hashset Key Generation Tests

	[Fact]
	public void GetHashsetKey_WithSimpleKey_GeneratesCorrectFormat()
	{
		// This test verifies the hashset key format indirectly through adapter calls
		// Arrange
		var key = 42;

		// Act
		_collection.Remove(key);

		// Assert
		_hashsetAdapter.Received(1).Remove(new RedisKey("concurrent:entities|42"));
	}

	[Fact]
	public void GetHashsetKey_WithCustomKeyFactory_UsesFactoryOutput()
	{
		// Arrange
		var customCollection = new ConcurrentEntityCollection<int, TestEntity>(
			"custom:space",
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_redisContext,
			key => $"prefix_{key}_suffix",
			_oldConcurrencyTokenSelector,
			_newConcurrencyTokenSelector);

		var key = 999;

		// Act
		customCollection.Remove(key);

		// Assert
		_hashsetAdapter.Received(1).Remove(new RedisKey("custom:space|prefix_999_suffix"));
	}

	#endregion

	#region Concurrency Control Integration Tests

	[Fact]
	public void ConcurrencyWorkflow_NewEntity_SetsCorrectConditionsAndFields()
	{
		// Arrange
		var key = 1000;
		var entity = new TestEntity { Id = 1000, Name = "Concurrent Entity", OldVersion = null, NewVersion = "v1" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|1000");
		var expectedVersionField = new RedisValue("ct");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(10), false);
		var database = Substitute.For<IDatabaseAsync>();

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		Func<IDatabaseAsync, Task>? capturedVersionCommand = null;
		var callCount = 0;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action =>
		{
			callCount++;
			if (callCount == 1) // First call is for version (SetVersion is called first)
				capturedVersionCommand = action;
		}));

		// Act
		_collection.Set(key, entity);

		// Assert
		// Verify entity field is set
		_hashsetAdapter.Received(1).SetField(expectedHashsetKey, new RedisValue("obj"), entity);
		// Verify concurrency token field is set via command
		Assert.NotNull(capturedVersionCommand);
		capturedVersionCommand(database);
		database.Received(1).HashSetAsync(expectedHashsetKey, expectedVersionField, "v1");
		// Verify KeyNotExists condition is added
		_redisContext.Received(1).AddCondition(Arg.Any<Condition>());
		// Verify version and expiration commands are added (version first, then expiration)
		_redisContext.Received(2).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void ConcurrencyWorkflow_ExistingEntity_SetsCorrectConditionsAndFields()
	{
		// Arrange
		var key = 2000;
		var entity = new TestEntity { Id = 2000, Name = "Updated Entity", OldVersion = "v1", NewVersion = "v2" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|2000");
		var expectedVersionField = new RedisValue("ct");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(10), false);
		var database = Substitute.For<IDatabaseAsync>();

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);

		Func<IDatabaseAsync, Task>? capturedVersionCommand = null;
		var callCount = 0;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action =>
		{
			callCount++;
			if (callCount == 1) // First call is for version (SetVersion is called first)
				capturedVersionCommand = action;
		}));

		// Act
		_collection.Set(key, entity);

		// Assert
		// Verify entity field is set
		_hashsetAdapter.Received(1).SetField(expectedHashsetKey, new RedisValue("obj"), entity);
		// Verify concurrency token field is set with new version via command
		Assert.NotNull(capturedVersionCommand);
		capturedVersionCommand(database);
		database.Received(1).HashSetAsync(expectedHashsetKey, expectedVersionField, "v2");
		// Verify HashEqual condition is added
		_redisContext.Received(1).AddCondition(Arg.Any<Condition>());
		// Verify version and expiration commands are added (version first, then expiration)
		_redisContext.Received(2).AddCommand(Arg.Any<Func<IDatabaseAsync, Task>>());
	}

	[Fact]
	public void CompleteWorkflow_SetTryReadRemove_WorksCorrectly()
	{
		// Arrange
		var key = 3000;
		var entity = new TestEntity { Id = 3000, Name = "Complete Workflow", OldVersion = null, NewVersion = "v1" };
		var expectedHashsetKey = new RedisKey("concurrent:entities|3000");
		var expectedVersionField = new RedisValue("ct");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), true);
		var readResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(entity));
		var database = Substitute.For<IDatabaseAsync>();

		_lifetimeProvider.GetCachingExpiration(key).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(expectedHashsetKey, new RedisValue("obj")).Returns(readResult);

		Func<IDatabaseAsync, Task>? capturedVersionCommand = null;
		var callCount = 0;
		_redisContext.AddCommand(Arg.Do<Func<IDatabaseAsync, Task>>(action =>
		{
			callCount++;
			if (callCount == 1) // First call is for version (SetVersion is called first)
				capturedVersionCommand = action;
		}));

		// Act & Assert - SetChild
		_collection.Set(key, entity);
		_hashsetAdapter.Received(1).SetField(expectedHashsetKey, new RedisValue("obj"), entity);
		Assert.NotNull(capturedVersionCommand);
		capturedVersionCommand(database);
		database.Received(1).HashSetAsync(expectedHashsetKey, expectedVersionField, "v1");

		// Act & Assert - TryReadChild (calls Touch internally)
		var result = _collection.TryRead(key);
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedHashsetKey, new RedisValue("obj"));
		_expirationUpdater.Received(1).UpdateLifetime(expectedHashsetKey, TimeSpan.FromMinutes(5));
		Assert.Same(GetTask(readResult), GetTask(result));

		// Act & Assert - RemoveChild
		_collection.Remove(key);
		_hashsetAdapter.Received(1).Remove(expectedHashsetKey);
	}

	#endregion
}