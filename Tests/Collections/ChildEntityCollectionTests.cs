using RedisDatabase;
using RedisDatabase.Adapters;
using RedisDatabase.Collections;
using RedisDatabase.ExpirationUpdaters;
using RedisDatabase.LifetimeProvider;
using NSubstitute;
using StackExchange.Redis;
using System.Reflection;

namespace Tests.Collections;

public class ChildEntityCollectionTests
{
	private readonly IHashsetAdapter _hashsetAdapter;
	private readonly ILifetimeProvider _lifetimeProvider;
	private readonly IExpirationUpdater _expirationUpdater;
	private readonly Func<int, string> _uniqueParentKeyFactory;
	private readonly Func<string, string> _uniqueChildKeyFactory;
	private readonly ChildEntityCollection<int, string, TestEntity> _collection;
	private const string _keySpace = "test:parent";
	private const string _childKeyPrefix = "child";

	public ChildEntityCollectionTests()
	{
		_hashsetAdapter = Substitute.For<IHashsetAdapter>();
		_lifetimeProvider = Substitute.For<ILifetimeProvider>();
		_expirationUpdater = Substitute.For<IExpirationUpdater>();
		_uniqueParentKeyFactory = parentKey => parentKey.ToString();
		_uniqueChildKeyFactory = childKey => childKey;

		_collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueParentKeyFactory,
			_uniqueChildKeyFactory);
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
		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueParentKeyFactory,
			_uniqueChildKeyFactory);

		Assert.NotNull(collection);
	}

	#endregion

	#region TryGetChild Tests

	[Fact]
	public async Task TryGetChild_WithExistingEntity_ReturnsEntity()
	{
		// Arrange
		var parentKey = 123;
		var childKey = "item1";
		var expectedParentCacheKey = new RedisKey("test:parent|123");
		var expectedChildCacheKey = new RedisValue("child|item1");
		var expectedEntity = new TestEntity { Id = 1, Name = "Test Entity" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGetChild(parentKey, childKey);

		// Assert
		Assert.NotNull(result);
		Assert.Same(expectedEntity, result);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
	}

	[Fact]
	public async Task TryGetChild_WithNonExistingEntity_ReturnsNull()
	{
		// Arrange
		var parentKey = 456;
		var childKey = "item2";
		var expectedParentCacheKey = new RedisKey("test:parent|456");
		var expectedChildCacheKey = new RedisValue("child|item2");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		var result = await _collection.TryGetChild(parentKey, childKey);

		// Assert
		Assert.Null(result);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
	}

	[Fact]
	public async Task TryGetChild_WithExtendExpirationOnReadsTrue_UpdatesParentLifetime()
	{
		// Arrange
		var parentKey = 789;
		var childKey = "item3";
		var expectedParentCacheKey = new RedisKey("test:parent|789");
		var expectedChildCacheKey = new RedisValue("child|item3");
		var expiration = TimeSpan.FromMinutes(10);
		var lifetime = new CachingLifetime(expiration, true);
		var expectedEntity = new TestEntity { Id = 3, Name = "Test" };

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGetChild(parentKey, childKey);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedParentCacheKey, expiration);
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGetChild_WithExtendExpirationOnReadsFalse_DoesNotUpdateParentLifetime()
	{
		// Arrange
		var parentKey = 321;
		var childKey = "item4";
		var expectedParentCacheKey = new RedisKey("test:parent|321");
		var expectedChildCacheKey = new RedisValue("child|item4");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(15), false);
		var expectedEntity = new TestEntity { Id = 4, Name = "Test" };

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGetChild(parentKey, childKey);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
		Assert.Same(expectedEntity, result);
	}

	[Fact]
	public async Task TryGetChild_CallsAdapterWithCorrectKeys()
	{
		// Arrange
		var parentKey = 999;
		var childKey = "item5";
		var expectedParentCacheKey = new RedisKey("test:parent|999");
		var expectedChildCacheKey = new RedisValue("child|item5");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await _collection.TryGetChild(parentKey, childKey);

		// Assert
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
	}

	[Fact]
	public async Task TryGetChild_WithDifferentParentKeys_GeneratesDifferentParentCacheKeys()
	{
		// Arrange
		var parentKey1 = 100;
		var parentKey2 = 200;
		var childKey = "item";
		var expectedParentCacheKey1 = new RedisKey("test:parent|100");
		var expectedParentCacheKey2 = new RedisKey("test:parent|200");
		var expectedChildCacheKey = new RedisValue("child|item");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await _collection.TryGetChild(parentKey1, childKey);
		await _collection.TryGetChild(parentKey2, childKey);

		// Assert
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey1, expectedChildCacheKey);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey2, expectedChildCacheKey);
	}

	[Fact]
	public async Task TryGetChild_WithDifferentChildKeys_GeneratesDifferentChildCacheKeys()
	{
		// Arrange
		var parentKey = 100;
		var childKey1 = "item1";
		var childKey2 = "item2";
		var expectedParentCacheKey = new RedisKey("test:parent|100");
		var expectedChildCacheKey1 = new RedisValue("child|item1");
		var expectedChildCacheKey2 = new RedisValue("child|item2");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await _collection.TryGetChild(parentKey, childKey1);
		await _collection.TryGetChild(parentKey, childKey2);

		// Assert
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey1);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey2);
	}

	[Fact]
	public async Task TryGetChild_WhenAdapterThrows_PropagatesException()
	{
		// Arrange
		var parentKey = 111;
		var childKey = "item6";
		var expectedParentCacheKey = new RedisKey("test:parent|111");
		var expectedChildCacheKey = new RedisValue("child|item6");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedException = new InvalidOperationException("Redis error");

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns<Task<TestEntity?>>(_ => throw expectedException);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _collection.TryGetChild(parentKey, childKey));
		Assert.Same(expectedException, exception);
	}

	[Fact]
	public async Task TryGetChild_WithCustomParentKeyFactory_UsesFactoryOutput()
	{
		// Arrange
		var parentKey = 777;
		var childKey = "item7";
		var customParentKeyFactory = Substitute.For<Func<int, string>>();
		customParentKeyFactory(parentKey).Returns("custom777");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			customParentKeyFactory,
			_uniqueChildKeyFactory);

		var expectedParentCacheKey = new RedisKey("test:parent|custom777");
		var expectedChildCacheKey = new RedisValue("child|item7");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await collection.TryGetChild(parentKey, childKey);

		// Assert
		customParentKeyFactory.Received(1)(parentKey);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
	}

	[Fact]
	public async Task TryGetChild_WithCustomChildKeyFactory_UsesFactoryOutput()
	{
		// Arrange
		var parentKey = 888;
		var childKey = "item8";
		var customChildKeyFactory = Substitute.For<Func<string, string>>();
		customChildKeyFactory(childKey).Returns("customChild");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueParentKeyFactory,
			customChildKeyFactory);

		var expectedParentCacheKey = new RedisKey("test:parent|888");
		var expectedChildCacheKey = new RedisValue("child|customChild");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(null));

		// Act
		await collection.TryGetChild(parentKey, childKey);

		// Assert
		customChildKeyFactory.Received(1)(childKey);
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
	}

	[Fact]
	public async Task TryGetChild_MultipleCalls_CallsAdapterForEach()
	{
		// Arrange
		var parentKey = 500;
		var childKeys = new[] { "item1", "item2", "item3" };
		var entities = childKeys.Select((k, i) => new TestEntity { Id = i + 1, Name = $"Entity {i + 1}" }).ToArray();
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedParentCacheKey = new RedisKey("test:parent|500");

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		for (int i = 0; i < childKeys.Length; i++)
		{
			var childCacheKey = new RedisValue($"child|{childKeys[i]}");
			_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, childCacheKey).Returns(Task.FromResult<TestEntity?>(entities[i]));
		}

		// Act
		var results = new List<TestEntity?>();
		foreach (var childKey in childKeys)
		{
			results.Add(await _collection.TryGetChild(parentKey, childKey));
		}

		// Assert
		for (int i = 0; i < childKeys.Length; i++)
		{
			var expectedChildCacheKey = new RedisValue($"child|{childKeys[i]}");
			await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
			Assert.Same(entities[i], results[i]);
		}
	}

	[Fact]
	public async Task TryGetChild_ExecutesImmediately_WithoutBatching()
	{
		// Arrange
		var parentKey = 666;
		var childKey = "item9";
		var expectedParentCacheKey = new RedisKey("test:parent|666");
		var expectedChildCacheKey = new RedisValue("child|item9");
		var expectedEntity = new TestEntity { Id = 9, Name = "Test" };
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(Task.FromResult<TestEntity?>(expectedEntity));

		// Act
		var result = await _collection.TryGetChild(parentKey, childKey);

		// Assert
		// Verify TryGetField is called, not TryReadField (which would be for batching)
		await _hashsetAdapter.Received(1).TryGetField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
		_hashsetAdapter.DidNotReceive().TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>());
		Assert.Same(expectedEntity, result);
	}

	#endregion

	#region TryReadChild Tests

	[Fact]
	public void TryReadChild_WithValidKeys_CallsAdapterWithCorrectKeys()
	{
		// Arrange
		var parentKey = 123;
		var childKey = "item1";
		var expectedParentCacheKey = new RedisKey("test:parent|123");
		var expectedChildCacheKey = new RedisValue("child|item1");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(expectedResult);

		// Act
		var result = _collection.TryReadChild(parentKey, childKey);

		// Assert
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey);
		_lifetimeProvider.Received(1).GetCachingExpiration(parentKey);
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryReadChild_WithExtendExpirationOnReadsTrue_UpdatesParentLifetime()
	{
		// Arrange
		var parentKey = 456;
		var childKey = "item2";
		var expectedParentCacheKey = new RedisKey("test:parent|456");
		var expectedChildCacheKey = new RedisValue("child|item2");
		var expiration = TimeSpan.FromMinutes(10);
		var lifetime = new CachingLifetime(expiration, true);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(expectedResult);

		// Act
		var result = _collection.TryReadChild(parentKey, childKey);

		// Assert
		_expirationUpdater.Received(1).UpdateLifetime(expectedParentCacheKey, expiration);
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryReadChild_WithExtendExpirationOnReadsFalse_DoesNotUpdateParentLifetime()
	{
		// Arrange
		var parentKey = 789;
		var childKey = "item3";
		var expectedParentCacheKey = new RedisKey("test:parent|789");
		var expectedChildCacheKey = new RedisValue("child|item3");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey).Returns(expectedResult);

		// Act
		var result = _collection.TryReadChild(parentKey, childKey);

		// Assert
		_expirationUpdater.DidNotReceive().UpdateLifetime(Arg.Any<RedisKey>(), Arg.Any<TimeSpan>());
		Assert.Same(GetTask(expectedResult), GetTask(result));
	}

	[Fact]
	public void TryReadChild_WithDifferentParentKeys_GeneratesDifferentParentCacheKeys()
	{
		// Arrange
		var parentKey1 = 100;
		var parentKey2 = 200;
		var childKey = "item";
		var expectedParentCacheKey1 = new RedisKey("test:parent|100");
		var expectedParentCacheKey2 = new RedisKey("test:parent|200");
		var expectedChildCacheKey = new RedisValue("child|item");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(expectedResult);

		// Act
		_collection.TryReadChild(parentKey1, childKey);
		_collection.TryReadChild(parentKey2, childKey);

		// Assert
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedParentCacheKey1, expectedChildCacheKey);
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedParentCacheKey2, expectedChildCacheKey);
	}

	[Fact]
	public void TryReadChild_WithDifferentChildKeys_GeneratesDifferentChildCacheKeys()
	{
		// Arrange
		var parentKey = 100;
		var childKey1 = "item1";
		var childKey2 = "item2";
		var expectedParentCacheKey = new RedisKey("test:parent|100");
		var expectedChildCacheKey1 = new RedisValue("child|item1");
		var expectedChildCacheKey2 = new RedisValue("child|item2");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		var expectedResult = new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity()));

		_lifetimeProvider.GetCachingExpiration(Arg.Any<int>()).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(expectedResult);

		// Act
		_collection.TryReadChild(parentKey, childKey1);
		_collection.TryReadChild(parentKey, childKey2);

		// Assert
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey1);
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedParentCacheKey, expectedChildCacheKey2);
	}

	#endregion

	#region SetChild Tests

	[Fact]
	public void SetChild_WithValidKeys_CallsAdapterWithCorrectKeys()
	{
		// Arrange
		var parentKey = 123;
		var childKey = "item1";
		var entity = new TestEntity { Id = 1, Name = "Test" };
		var expectedParentCacheKey = new RedisKey("test:parent|123");
		var expectedChildCacheKey = new RedisValue("child|item1");

		// Act
		_collection.SetChild(parentKey, childKey, entity);

		// Assert
		_hashsetAdapter.Received(1).SetField(expectedParentCacheKey, expectedChildCacheKey, entity);
	}

	[Fact]
	public void SetChild_WithDifferentParentKeys_CallsAdapterWithDifferentParentCacheKeys()
	{
		// Arrange
		var parentKey1 = 100;
		var parentKey2 = 200;
		var childKey = "item";
		var entity = new TestEntity { Id = 1, Name = "Test" };
		var expectedParentCacheKey1 = new RedisKey("test:parent|100");
		var expectedParentCacheKey2 = new RedisKey("test:parent|200");
		var expectedChildCacheKey = new RedisValue("child|item");

		// Act
		_collection.SetChild(parentKey1, childKey, entity);
		_collection.SetChild(parentKey2, childKey, entity);

		// Assert
		_hashsetAdapter.Received(1).SetField(expectedParentCacheKey1, expectedChildCacheKey, entity);
		_hashsetAdapter.Received(1).SetField(expectedParentCacheKey2, expectedChildCacheKey, entity);
	}

	[Fact]
	public void SetChild_WithDifferentChildKeys_CallsAdapterWithDifferentChildCacheKeys()
	{
		// Arrange
		var parentKey = 100;
		var childKey1 = "item1";
		var childKey2 = "item2";
		var entity = new TestEntity { Id = 1, Name = "Test" };
		var expectedParentCacheKey = new RedisKey("test:parent|100");
		var expectedChildCacheKey1 = new RedisValue("child|item1");
		var expectedChildCacheKey2 = new RedisValue("child|item2");

		// Act
		_collection.SetChild(parentKey, childKey1, entity);
		_collection.SetChild(parentKey, childKey2, entity);

		// Assert
		_hashsetAdapter.Received(1).SetField(expectedParentCacheKey, expectedChildCacheKey1, entity);
		_hashsetAdapter.Received(1).SetField(expectedParentCacheKey, expectedChildCacheKey2, entity);
	}

	#endregion

	#region RemoveChild Tests

	[Fact]
	public void RemoveChild_WithValidKeys_CallsAdapterWithCorrectKeys()
	{
		// Arrange
		var parentKey = 123;
		var childKey = "item1";
		var expectedParentCacheKey = new RedisKey("test:parent|123");
		var expectedChildCacheKey = new RedisValue("child|item1");

		// Act
		_collection.RemoveChild(parentKey, childKey);

		// Assert
		_hashsetAdapter.Received(1).RemoveField(expectedParentCacheKey, expectedChildCacheKey);
	}

	[Fact]
	public void RemoveChild_WithDifferentParentKeys_CallsAdapterWithDifferentParentCacheKeys()
	{
		// Arrange
		var parentKey1 = 100;
		var parentKey2 = 200;
		var childKey = "item";
		var expectedParentCacheKey1 = new RedisKey("test:parent|100");
		var expectedParentCacheKey2 = new RedisKey("test:parent|200");
		var expectedChildCacheKey = new RedisValue("child|item");

		// Act
		_collection.RemoveChild(parentKey1, childKey);
		_collection.RemoveChild(parentKey2, childKey);

		// Assert
		_hashsetAdapter.Received(1).RemoveField(expectedParentCacheKey1, expectedChildCacheKey);
		_hashsetAdapter.Received(1).RemoveField(expectedParentCacheKey2, expectedChildCacheKey);
	}

	[Fact]
	public void RemoveChild_WithDifferentChildKeys_CallsAdapterWithDifferentChildCacheKeys()
	{
		// Arrange
		var parentKey = 100;
		var childKey1 = "item1";
		var childKey2 = "item2";
		var expectedParentCacheKey = new RedisKey("test:parent|100");
		var expectedChildCacheKey1 = new RedisValue("child|item1");
		var expectedChildCacheKey2 = new RedisValue("child|item2");

		// Act
		_collection.RemoveChild(parentKey, childKey1);
		_collection.RemoveChild(parentKey, childKey2);

		// Assert
		_hashsetAdapter.Received(1).RemoveField(expectedParentCacheKey, expectedChildCacheKey1);
		_hashsetAdapter.Received(1).RemoveField(expectedParentCacheKey, expectedChildCacheKey2);
	}

	#endregion

	#region Key Generation Tests

	[Fact]
	public void TryReadChild_UsesParentKeyFactoryToGenerateParentCacheKey()
	{
		// Arrange
		var parentKey = 999;
		var childKey = "test";
		var customParentKeyFactory = Substitute.For<Func<int, string>>();
		customParentKeyFactory(parentKey).Returns("custom999");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			customParentKeyFactory,
			_uniqueChildKeyFactory);

		var expectedParentCacheKey = new RedisKey("test:parent|custom999");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
			.Returns(new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity())));

		// Act
		collection.TryReadChild(parentKey, childKey);

		// Assert
		customParentKeyFactory.Received(1)(parentKey);
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(expectedParentCacheKey, Arg.Any<RedisValue>());
	}

	[Fact]
	public void TryReadChild_UsesChildKeyFactoryToGenerateChildCacheKey()
	{
		// Arrange
		var parentKey = 100;
		var childKey = "test";
		var customChildKeyFactory = Substitute.For<Func<string, string>>();
		customChildKeyFactory(childKey).Returns("customChild");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueParentKeyFactory,
			customChildKeyFactory);

		var expectedChildCacheKey = new RedisValue("child|customChild");
		var lifetime = new CachingLifetime(TimeSpan.FromMinutes(5), false);
		_lifetimeProvider.GetCachingExpiration(parentKey).Returns(lifetime);
		_hashsetAdapter.TryReadField<TestEntity>(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
			.Returns(new ReadResult<TestEntity>(Task.FromResult<TestEntity?>(new TestEntity())));

		// Act
		collection.TryReadChild(parentKey, childKey);

		// Assert
		customChildKeyFactory.Received(1)(childKey);
		_hashsetAdapter.Received(1).TryReadField<TestEntity>(Arg.Any<RedisKey>(), expectedChildCacheKey);
	}

	[Fact]
	public void SetChild_UsesParentKeyFactoryToGenerateParentCacheKey()
	{
		// Arrange
		var parentKey = 999;
		var childKey = "test";
		var entity = new TestEntity();
		var customParentKeyFactory = Substitute.For<Func<int, string>>();
		customParentKeyFactory(parentKey).Returns("custom999");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			customParentKeyFactory,
			_uniqueChildKeyFactory);

		var expectedParentCacheKey = new RedisKey("test:parent|custom999");

		// Act
		collection.SetChild(parentKey, childKey, entity);

		// Assert
		customParentKeyFactory.Received(1)(parentKey);
		_hashsetAdapter.Received(1).SetField(expectedParentCacheKey, Arg.Any<RedisValue>(), entity);
	}

	[Fact]
	public void SetChild_UsesChildKeyFactoryToGenerateChildCacheKey()
	{
		// Arrange
		var parentKey = 100;
		var childKey = "test";
		var entity = new TestEntity();
		var customChildKeyFactory = Substitute.For<Func<string, string>>();
		customChildKeyFactory(childKey).Returns("customChild");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueParentKeyFactory,
			customChildKeyFactory);

		var expectedChildCacheKey = new RedisValue("child|customChild");

		// Act
		collection.SetChild(parentKey, childKey, entity);

		// Assert
		customChildKeyFactory.Received(1)(childKey);
		_hashsetAdapter.Received(1).SetField(Arg.Any<RedisKey>(), expectedChildCacheKey, entity);
	}

	[Fact]
	public void RemoveChild_UsesParentKeyFactoryToGenerateParentCacheKey()
	{
		// Arrange
		var parentKey = 999;
		var childKey = "test";
		var customParentKeyFactory = Substitute.For<Func<int, string>>();
		customParentKeyFactory(parentKey).Returns("custom999");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			customParentKeyFactory,
			_uniqueChildKeyFactory);

		var expectedParentCacheKey = new RedisKey("test:parent|custom999");

		// Act
		collection.RemoveChild(parentKey, childKey);

		// Assert
		customParentKeyFactory.Received(1)(parentKey);
		_hashsetAdapter.Received(1).RemoveField(expectedParentCacheKey, Arg.Any<RedisValue>());
	}

	[Fact]
	public void RemoveChild_UsesChildKeyFactoryToGenerateChildCacheKey()
	{
		// Arrange
		var parentKey = 100;
		var childKey = "test";
		var customChildKeyFactory = Substitute.For<Func<string, string>>();
		customChildKeyFactory(childKey).Returns("customChild");

		var collection = new ChildEntityCollection<int, string, TestEntity>(
			_keySpace,
			_childKeyPrefix,
			_hashsetAdapter,
			_lifetimeProvider,
			_expirationUpdater,
			_uniqueParentKeyFactory,
			customChildKeyFactory);

		var expectedChildCacheKey = new RedisValue("child|customChild");

		// Act
		collection.RemoveChild(parentKey, childKey);

		// Assert
		customChildKeyFactory.Received(1)(childKey);
		_hashsetAdapter.Received(1).RemoveField(Arg.Any<RedisKey>(), expectedChildCacheKey);
	}

	#endregion
}
