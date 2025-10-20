# Claude Context - BlueBrown.Sportsbook.Redis

## Table of Contents

- [Project Overview](#project-overview)
- [Documentation Structure](#documentation-structure)
- [Feature: Database Operations](#feature-database-operations)
  - [Key Components](#database-operations---key-components)
  - [Architecture](#database-operations---architecture)
  - [Testing](#database-operations---testing)
  - [Recent Work](#database-operations---recent-work)
- [Feature: Database Size Collectors](#feature-database-size-collectors)
  - [Key Components](#database-size-collectors---key-components)
  - [Architecture](#database-size-collectors---architecture)
  - [Testing](#database-size-collectors---testing)
  - [Recent Work](#database-size-collectors---recent-work)
- [Cross-Cutting Concerns](#cross-cutting-concerns)
  - [Logging Strategy](#logging-strategy)
  - [Testing Requirements](#testing-requirements)
  - [Dependencies](#dependencies)
- [Current Status Summary](#current-status-summary)
- [Usage Examples](#usage-examples)

---

## Project Overview

This is an extension package for the BlueBrown.Sportsbook.Common library that provides comprehensive Redis functionality including:

- **Database Operations** - Entity caching, collections, streams, and data persistence
- **Database Size Monitoring** - Redis keyspace metrics collection

The library is designed as a multi-feature package where each feature is self-contained but shares common infrastructure.

## Documentation Structure

The project maintains organized documentation at multiple levels:

- **[README.md](./README.md)** - High-level library overview and feature list
- **[Database/README.md](./Database/README.md)** - Complete guide to entity collections, streams, and caching
- **[DatabaseSizeCollectors/README.md](./DatabaseSizeCollectors/README.md)** - Redis keyspace metrics collection documentation
- **[CLAUDE.md](./CLAUDE.md)** - This file - Technical architecture and development documentation

---

## Feature: Database Operations

**Location:** `Database\` namespace

Provides core Redis database functionality through adapter patterns, collections, and streams.

### Database Operations - Key Components

#### Core Infrastructure

- **RedisContext**: Central context for managing Redis operations, transactions, and batched commands
- **ReadResult<T>**: Generic result wrapper for handling asynchronous Redis read operations
- **IExpirationUpdater**: Interface for managing Redis key expiration policies

#### Exception Types

- **RedisStackExchangeException**: Wraps all StackExchange.Redis library exceptions (infrastructure/communication errors)
- **RedisConflictException**: Business logic conflicts from transaction condition failures (NOT wrapped)

#### Redis Adapters

- **StringAdapter**: Handles Redis string operations with object serialization, supports both immediate (`TryGet`) and deferred (`TryRead`) execution patterns
- **HashsetAdapter**: Manages Redis hash operations with field-level access, supports both immediate (`TryGetField`) and deferred (`TryReadField`) execution patterns
- **RedisStreamAdapter**: Provides Redis Streams functionality with consumer groups and direct database operations
- **ResultReader**: Centralized deserialization handler with error logging and corrupted key cleanup

#### Collections

- **EntityCollection**: Simple caching using Redis strings, supports both immediate (`TryGet`) and deferred (`TryRead`) execution patterns
- **ConcurrentEntityCollection**: Optimistic concurrency control with version tokens, supports both immediate (`TryGet`) and deferred (`TryRead`) execution patterns, SetVersion executes before SetField for consistency
- **ChildEntityCollection**: Hierarchical parent-child data structures using Redis hashes, supports both immediate (`TryGetChild`) and deferred (`TryReadChild`) execution patterns

#### Serialization Infrastructure

- **IRedisSerializer**: Interface for object serialization/deserialization to Redis
- **RedisSerializerSignatureDecorator**: Decorator that adds type signature validation to detect schema changes

#### Builder Pattern

- **EntityCollectionBuilder** + Steps (3 steps)
- **ConcurrentEntityCollectionBuilder** + Steps (5 steps)
- **ChildEntityCollectionBuilder** + Steps (6 steps)
- **StreamAdapterBuilder** + Steps (3 steps)

#### Factory Pattern

- **CollectionsFactory**: Registration-based factory with builder caching for performance and support for named registrations

### Database Operations - Architecture

#### RedisContext

The central orchestrator for Redis operations that supports:

- **Transaction Management**: Conditional operations with multiple commands
- **Batch Operations**: Parallel execution of multiple Redis commands
- **Command Queuing**: Deferred execution pattern for optimal performance
- **Expiration Management**: Integrated key lifetime handling

```csharp
public class RedisContext : IRedisContext
{
    public IDatabase Database { get; }
    public IBatch Batch { get; }

    public Task<T> AddBatch<T>(Func<IBatch, Task<T>> action);
    public void AddCommand(Func<IDatabaseAsync, Task> action);
    public void AddCondition(Condition condition);
    public void SetExpiration(RedisKey key, TimeSpan? expiration);
    public Task Commit();
    public Task ExecuteBatch();
}
```

#### Adapter Pattern Implementation

All Redis adapters follow a consistent deferred execution pattern:

1. **Command Registration**: Operations are registered as lambda functions
2. **Serialization Deferral**: Object serialization happens at execution time
3. **Context Integration**: All operations go through RedisContext for consistency
4. **Exception Safety**: Proper parameter validation and error handling

#### ResultReader Pattern

The `ResultReader` class provides centralized deserialization with error handling:

- **Deserialization**: Converts Redis byte arrays to typed objects
- **Error Logging**: Logs deserialization failures with full context
- **Corrupted Key Cleanup**: Automatically deletes keys that fail deserialization
- **Null Handling**: Returns null for missing Redis values without logging errors

```csharp
public class ResultReader : IResultReader
{
    private readonly ILogger _logger;

    public TType? Read<TType>(IRedisContext context, IRedisSerializer serializer, RedisKey key, Task<RedisValue> task)
    {
        if (task.Result.IsNull)
            return null;

        try
        {
            return serializer.Deserialize<TType>(task.Result!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing key {key}", key);
            context.Database.KeyDelete(key);
            return null;
        }
    }
}
```

#### Collections Factory Architecture

- **Registration Pattern**: Explicit registration during startup, fast retrieval at runtime
- **Named Registrations**: Support for multiple registrations of the same type with different configurations using optional name parameter (defaults to "default")
- **Partial Class Organization**: Split into 5 files for maintainability
- **Builder Caching**: Thread-safe caching using `ConcurrentDictionary` with keys combining type information and registration name
- **Validation**: Prevents duplicate registration for the same type+name combination and enforces registration-before-use

#### Step Builder Pattern

All builders implement a step-by-step configuration pattern:

- **Type Safety**: Compile-time enforcement of configuration order
- **Required Configuration**: Each step returns the next step interface
- **Fluent API**: Natural, readable configuration syntax
- **Signature Serialization**: `WithSignaturedSerializer` method on all builders

#### Exception Handling Strategy

The library uses a **dual-exception strategy** to distinguish between infrastructure failures and business logic conflicts:

**RedisStackExchangeException (Infrastructure Layer):**
- Wraps ALL exceptions originating from StackExchange.Redis library calls
- Thrown by: `ExecuteBatch()`, `CommitIndividually()`, `CommitTransactional()` (on ExecuteAsync failures only)
- Indicates: Redis connection issues, network timeouts, server errors
- Consumer action: Implement retry logic, circuit breakers, or fallback strategies

**RedisConflictException (Business Logic Layer):**
- Thrown when transaction conditions fail (optimistic concurrency violations)
- Thrown by: `CommitTransactional()` when `ExecuteAsync()` returns false
- **NOT wrapped** in RedisStackExchangeException
- Indicates: Version mismatches, key exists/not exists condition failures
- Consumer action: Reload fresh data and retry with updated version

**Implementation in RedisContext:**
```csharp
private async Task CommitTransactional()
{
    var transaction = _db.CreateTransaction();

    foreach (var condition in _conditions)
        transaction.AddCondition(condition);

    foreach (var action in _actions)
        _ = action(transaction);

    bool committed;

    try
    {
        committed = await transaction.ExecuteAsync();
    }
    catch (Exception ex)
    {
        throw new RedisStackExchangeException(ex.Message, ex);  // ✓ Wrap StackExchange errors
    }

    if (!committed)
        throw new RedisConflictException($"Redis transaction failed - VersionConflict");  // ✓ NOT wrapped
}
```

**Benefits:**
- Clear separation between transient infrastructure issues and deterministic business conflicts
- Enables targeted error handling strategies
- Infrastructure failures can trigger circuit breakers
- Business conflicts can trigger optimistic retry logic

### Database Operations - Testing

**Location:** `Tests/Database/` (696+ tests)

#### Test Suites

- **RedisContextTests.cs**: 30 tests covering context lifecycle, transactions, batching, exception wrapping
- **ReadResultTests.cs**: 7 tests covering asynchronous result handling with private field access via reflection
- **StringAdapterTests.cs**: 24 tests covering Redis string operations including immediate (`TryGet`) and deferred (`TryRead`) patterns
- **HashsetAdapterTests.cs**: 26 tests covering Redis hash operations including immediate (`TryGetField`) and deferred (`TryReadField`) patterns
- **StreamAdapterTests.cs**: 21 tests covering Redis Streams functionality
- **ResultReaderTests.cs**: 16 tests covering deserialization and error handling
- **EntityCollectionTests.cs**: 30 tests covering cache operations including immediate retrieval with expiration extension
- **ConcurrentEntityCollectionTests.cs**: 39 tests covering optimistic concurrency, execution order, and immediate retrieval patterns
- **ChildEntityCollectionTests.cs**: Tests covering hierarchical collections with immediate (`TryGetChild`) and deferred (`TryReadChild`) patterns
- **BackgroundExpirationUpdaterTests.cs**: 26 tests covering time-based scheduling, half-life optimization, and thread-safety
- **Factory Tests**: Comprehensive coverage including `WithSignaturedSerializer`, defensive type checking, and named registration patterns (56 tests total)
- **Builder Tests**: Complete coverage for all collection types
- **Step Builder Tests**: Complete coverage for all serializer steps

#### Key Testing Areas

1. **Constructor Validation**: Parameter null checks with ArgumentNullException
2. **Deferred Execution**: Command registration without immediate serialization (TryRead patterns)
3. **Immediate Execution**: Direct database operations without batching (TryGet patterns)
4. **Context Integration**: Verifying proper AddCommand/AddBatch calls
5. **ReadResult Behavior**: Async task handling with private field access via reflection
6. **Collection Logic**: Deduplication, version management, expiration, execution order
7. **Streams Integration**: Consumer groups, message ordering, acknowledgments
8. **Factory Defensive Testing**: Type safety checks using reflection to simulate internal state corruption
9. **Expiration Update Behavior**: Last-write-wins semantics in BackgroundExpirationUpdater
10. **Named Registrations**: Multiple registrations of same types with different names, name-based retrieval, duplicate name validation

### Database Operations - Recent Work

#### Architecture Evolution

- **Operations Layer Elimination**: Moved RedisStreamAdapter operations directly into adapter
- **ResultReader Integration**: Added centralized deserialization with error handling
- **Direct Database Integration**: StreamAdapter tests mock IDatabase directly

#### Builder Pattern Implementation

- **Step Builders**: Implemented for all collection types and streams
- **Signature Serialization**: Added `WithSignaturedSerializer` to all builders
- **Type Safety**: Compile-time enforcement of configuration order
- **XML Documentation**: Complete documentation on all builder step interfaces
  - EntityCollectionBuilderSteps: 3 steps
  - ConcurrentEntityCollectionBuilderSteps: 5 steps
  - ChildEntityCollectionBuilderSteps: 6 steps
  - StreamAdapterBuilderSteps: 3 steps

#### Collections Factory

- **Registration Pattern**: Explicit registration with builder caching
- **Partial Class Split**: 5 files for better organization
- **Performance Optimization**: Cached builders for fast runtime retrieval

#### Comprehensive Logging

- **RedisStreamAdapter**: Debug/Trace/Warning/Error levels throughout
- **Performance Awareness**: Appropriate log levels for high-frequency operations
- **Context Preservation**: All logs include operational context

#### Immediate Execution Pattern (TryGet/TryGetField/TryGetChild)

- **StringAdapter.TryGet**: Added immediate execution method that bypasses batching
- **HashsetAdapter.TryGetField**: Added immediate field retrieval without deferred execution
- **EntityCollection.TryGet**: Immediate entity retrieval with optional expiration extension
- **ConcurrentEntityCollection.TryGet**: Immediate retrieval with version management
- **ChildEntityCollection.TryGetChild**: Immediate child entity retrieval
- **XML Documentation**: Complete documentation on all interfaces following established guidelines
- **Comprehensive Testing**: 49 new tests covering all immediate execution patterns
  - StringAdapter: 8 tests for TryGet
  - HashsetAdapter: 9 tests for TryGetField
  - EntityCollection: 10 tests for TryGet
  - ConcurrentEntityCollection: 10 tests for TryGet
  - ChildEntityCollection: 12 tests for TryGetChild

#### ReadResult API Changes

- **ReadResult.Task Field**: Changed from public property to private field for proper encapsulation
- **Test Infrastructure**: Added reflection-based `GetTask<T>` helper methods in all affected test files
- **Pattern**: `typeof(ReadResult<T>).GetField("_task", BindingFlags.NonPublic | BindingFlags.Instance)`
- **Files Updated**: 6 test files fixed (30 compilation errors resolved)

#### ConcurrentEntityCollection Execution Order

- **SetVersion Before SetField**: Changed execution order in `Set()` method for consistency
- **Version Command Index**: Now executes first (index 1) before entity serialization
- **Expiration Command Index**: Now executes last (index 2) after all entity operations
- **Tests Updated**: Fixed 6 tests to reflect new command execution order

#### BackgroundExpirationUpdater Behavior Change

- **Removed _alreadyTouchedKeys**: Eliminated HashSet tracking for first-write-wins behavior
- **Last-Write-Wins Semantics**: Multiple calls with same key now update the value
- **ConcurrentDictionary.AddOrUpdate**: Direct usage for simpler, more predictable behavior
- **Tests Updated**: 7 tests modified to expect last-write-wins behavior instead of first-write-wins

#### Factory Defensive Type Checking

- **Type Safety Tests**: Added defensive tests for all factory methods
- **Reflection-Based Testing**: Simulate internal state corruption to verify error handling
- **Files Updated**: 4 factory test files with defensive type check tests
  - RedisCollectionsFactoryCreateChildEntityCollectionTests
  - RedisCollectionsFactoryCreateStreamAdapterTests
  - RedisCollectionsFactoryCreateEntityCollectionTests
  - RedisCollectionsFactoryCreateConcurrentEntityCollectionTests

#### PriorityQueueScheduler Sliding Expiration Implementation

- **Architecture Change**: Moved from boolean `Accessed` flag to `DateTime? LastAccess` timestamp for precise sliding window expiration
- **Sliding Expiration Calculation**: New `GetSlidingExpiration()` method calculates remaining TTL: `Expiration - (referenceTime - LastAccess)`
- **Behavioral Changes**:
  - `ScheduleOrUpdate()` now sets `LastAccess = DateTime.UtcNow` to track when key was last accessed
  - `GetScheduleds()` returns sliding expiration value that decreases over time as key ages
  - Keys re-scheduled with `LastAccess = null` after first processing to enable cleanup detection
  - Supports negative expirations when elapsed time exceeds original TTL (implementation doesn't prevent this)
- **Memory Management**: Keys with `LastAccess = null` are removed on next processing cycle if not re-accessed
- **Test Coverage**: 27 comprehensive tests (all passing) covering:
  - Sliding expiration calculations with various elapsed times
  - Edge cases including zero elapsed time, large elapsed time, and negative expirations
  - LastAccess state transitions (set → null → removed)
  - Memory leak prevention with cleanup verification
  - Thread-safety and concurrent access patterns

#### PriorityQueueScheduler Testability Improvements

- **Time Injection**: `GetScheduleds()` now accepts `DateTime referenceTime` parameter instead of using `DateTime.UtcNow` internally
- **Deterministic Testing**: Tests now inject specific times for predictable, reproducible results
- **Eliminated Thread.Sleep()**: Removed all `Thread.Sleep()` calls from tests by controlling time through method parameters (except one comparative timing test)
- **Reduced Reflection**: Removed reflection-based time manipulation helpers
  - Removed: `ClearPriorityQueue`, `EnqueueToPriorityQueue`, `GetExpiration`, `GetAccessed`
  - Kept: `GetScheduledExpirations`, `GetScheduledUpdates`, `GetPriorityQueueCount` (for memory leak verification only)
- **Cleaner Tests**: More maintainable tests that don't rely on timing-sensitive operations
- **Production Code**: `BackgroundExpirationUpdater` passes `DateTime.UtcNow` to `GetScheduleds()`
- **Test Pattern**: Tests call `GetScheduleds()` with specific past/future times relative to scheduling time
- **Documentation**: Added comment explaining remaining reflection helpers are only for internal cleanup verification

#### Named Registrations Support

- **Optional Name Parameter**: All factory Register/Get methods now accept optional `name` parameter (defaults to "default")
- **Use Cases**: Enables multiple registrations of the same type with different configurations (e.g., different key spaces, serializers, or TTLs)
- **Key Generation**: Internal builder keys combine type information with registration name using format `{TypeInfo}#{name}`
- **Enhanced Error Messages**: Exception messages now include the registration name for clarity
- **XML Documentation**: Complete documentation on all Register/Get method name parameters
- **Comprehensive Testing**: 16 new tests covering named registration scenarios
  - Registering multiple collections with different names (4 tests)
  - Retrieving correct collection by name (4 tests)
  - Duplicate name validation (4 tests)
  - Unregistered name error handling (4 tests)
- **Defensive Test Fixes**: Updated reflection-based defensive tests to pass "default" name parameter

#### Exception Handling Implementation

- **RedisStackExchangeException**: New exception type that wraps all StackExchange.Redis library exceptions
- **Purpose**: Provides clear distinction between Redis infrastructure failures and application-layer errors
- **Implementation Locations**:
  - `RedisContext.ExecuteBatch()`: Wraps batch execution failures
  - `RedisContext.CommitIndividually()`: Wraps individual command execution failures
  - `RedisContext.CommitTransactional()`: Wraps transaction `ExecuteAsync()` failures only
- **RedisConflictException Preservation**: Business logic conflicts are NOT wrapped - thrown directly for targeted handling
- **Test Coverage**: 3 new tests in RedisContextTests covering exception wrapping scenarios
- **Documentation**: Comprehensive exception handling guide added to Database/README.md with:
  - Exception type descriptions and use cases
  - Exception hierarchy diagram
  - Comprehensive error handling patterns
  - Circuit breaker and retry logic examples
  - Best practices for monitoring and logging

---

## Feature: Database Size Collectors

**Location:** `DatabaseSizeCollectors\` namespace

Provides Redis keyspace metrics collection for monitoring.

### Database Size Collectors - Key Components

- **RedisSizeCollector**: Implements `IDatabaseSizeCollector` to collect Redis keyspace metrics
- **RedisSizeCollectorSettings**: Configuration for collector settings
- **ServiceCollectionExtensions**: Fluent registration with Common library

### Database Size Collectors - Architecture

#### Collection Process

1. Execute Redis `INFO KEYSPACE` command
2. Parse response to extract database-specific metrics
3. Report metrics via `IDatabaseMetricsConnector`

#### INFO Keyspace Parsing

**Expected Format:**
```
# Keyspace
db0:keys=100,expires=50,avg_ttl=3600000
```

**Parsing Logic:**
1. Split response into lines
2. Find lines starting with "db" + number + colon
3. Extract database number and compare with `IDatabase.Database`
4. Parse key-value pairs (keys, expires, avg_ttl)
5. Report `keys` and `expires` metrics (skip avg_ttl)

**Edge Cases Handled:**
- Empty or null responses
- Missing database entries
- Invalid number formats
- Malformed lines
- Extra colons in metric names
- Whitespace around values

#### Configuration

```csharp
public class RedisSizeCollectorSettings
{
    public RedisSizeCollectorSettings(
        IDatabase database,
        string collectorName,
        IDatabaseMetricsConnector metrics);
}
```

#### Collected Metrics

- **{CollectorName}.keys** - Total key count
- **{CollectorName}.expires** - Keys with expiration set

### Database Size Collectors - Testing

**Location:** `Tests/DatabaseSizeCollectors/` (74 tests)

#### Test Suites

- **RedisSizeCollectorTests.cs**: 66 comprehensive tests covering all scenarios
- **RedisSizeCollectorSettingsTests.cs**: Settings validation tests
- **ServiceCollectionExtensionsTests.cs**: DI registration tests

#### Key Testing Areas

1. **Constructor Validation**: Parameter null checks
2. **Collect Method Success**: Various INFO responses
3. **Edge Cases**: Empty responses, malformed data
4. **Error Scenarios**: Connection failures, parsing errors
5. **Integration-like Tests**: Mock Redis interactions

### Database Size Collectors - Recent Work

- **Complete Test Suite**: 66 RedisSizeCollector tests
- **FluentAssertions Removal**: Migrated to standard xUnit assertions
- **Bug Fixes**: Fixed parsing edge cases (colons, whitespace)
- **Documentation**: Created comprehensive README with examples

---

## Cross-Cutting Concerns

### Logging Strategy

All components implement structured logging with Microsoft.Extensions.Logging:

- **Debug Level**: Method entry, operations, processing details
- **Trace Level**: Individual operations, completions
- **Warning Level**: Conflicts, deserialization failures
- **Error Level**: Critical failures, exceptions, cleanup

**Best Practices:**
- Structured data with named parameters
- Context preservation (keys, IDs, names)
- Performance awareness for high-frequency operations
- Complete exception context

### Testing Requirements

**Standardized Across All Features:**

- **NO FluentAssertions**: Use only standard xUnit assertions
- **NO Over-engineering**: Focus on core functionality
- **100% Coverage**: Comprehensive line and branch coverage
- **Simple Patterns**: Clear, readable tests

**Assertion Patterns:**
```csharp
// ✅ Standard xUnit assertions
Assert.NotNull(result);
Assert.Equal(expected, actual);
Assert.True(condition);
Assert.Throws<ArgumentNullException>(() => method());

// ❌ Avoid FluentAssertions
// result.Should().NotBeNull();
```

**Mock Strategy:**
- **NSubstitute** for all interfaces
- **RedisResult.Create()** for simulating responses
- **Unsafe Code** for StreamGroupInfo in tests
- **TestLogger<T>** for internal class constraints

**Reflection Avoidance:**

Reflection should be **avoided in unit tests** whenever possible. Tests using reflection are brittle, harder to maintain, and often indicate a design issue.

**Guidelines:**

1. **Prefer Dependency Injection & Mocking**: Mock dependencies instead of accessing private state
   ```csharp
   // ❌ BAD: Using reflection to check internal state
   var field = typeof(MyClass).GetField("_internalState", BindingFlags.NonPublic | BindingFlags.Instance);
   var state = field.GetValue(instance);
   Assert.Equal(expectedState, state);

   // ✅ GOOD: Mock dependencies and verify interactions
   _mockDependency.Received(1).ExpectedMethod(expectedArgs);
   ```

2. **Use Event Capturing**: For event-driven code, capture event handlers instead of using reflection
   ```csharp
   // ❌ BAD: Using reflection to invoke private event handlers
   var method = typeof(MyClass).GetMethod("OnEventTriggered", BindingFlags.NonPublic);
   method.Invoke(instance, null);

   // ✅ GOOD: Capture and invoke event handlers naturally
   AsyncEventHandler? capturedHandler = null;
   _eventSource.OnTrigger += Arg.Do<AsyncEventHandler>(h => capturedHandler = h);
   var instance = new MyClass(_eventSource);
   await capturedHandler!(); // Trigger naturally
   ```

3. **Refactor for Testability**: If tests require reflection, consider refactoring the production code
   - Extract complex logic into separate classes with interfaces
   - Use dependency injection for better isolation
   - Make internal state observable through interfaces or events
   - Consider if private methods should be internal with `[InternalsVisibleTo]`

**When Reflection is Acceptable:**

Reflection is acceptable ONLY when:
- Testing framework-specific behavior (e.g., serialization/deserialization)
- Testing defensive code paths that check internal state corruption (factory type safety tests)
- No reasonable alternative exists and the reflection usage is well-documented

**Example Refactoring:**

**Before (requires reflection):**
```csharp
class BackgroundUpdater {
    private readonly Scheduler _scheduler = new();

    public void Update(string key) {
        _scheduler.Schedule(key);
    }
}

// Test requires reflection to verify _scheduler state
[Fact]
public void Test() {
    var updater = new BackgroundUpdater();
    updater.Update("key");

    var field = typeof(BackgroundUpdater).GetField("_scheduler", ...);
    var scheduler = field.GetValue(updater);
    // Check scheduler state via reflection...
}
```

**After (no reflection needed):**
```csharp
class BackgroundUpdater {
    private readonly IScheduler _scheduler;

    public BackgroundUpdater(IScheduler scheduler) {
        _scheduler = scheduler;
    }

    public void Update(string key) {
        _scheduler.Schedule(key);
    }
}

// Test uses mocking - no reflection
[Fact]
public void Test() {
    var mockScheduler = Substitute.For<IScheduler>();
    var updater = new BackgroundUpdater(mockScheduler);

    updater.Update("key");

    mockScheduler.Received(1).Schedule("key"); // Clean verification
}
```

**Recent Examples:**
- `BackgroundExpirationUpdaterTests`: Refactored to inject `IPriorityQueueScheduler<RedisKey>` dependency instead of accessing private `_scheduler` field via reflection
- `SchedulerPriorityQueueTests`: Refactored to inject time via `GetScheduleds(DateTime referenceTime)` parameter instead of using reflection to manipulate internal priority queue timing
- Event handler capturing: Changed from reflection-based method invocation to natural event triggering via `AsyncEventHandler` capture

### Dependencies

#### External Packages

- **StackExchange.Redis**: Redis connectivity and commands
- **BlueBrown.Sportsbook.Common**: Core infrastructure interfaces
- **TypeSignature**: For signature serialization decorator

#### Test Dependencies

- **NSubstitute**: Mocking framework
- **xUnit**: Testing framework
- **Unsafe Code Support**: AllowUnsafeBlocks enabled

---

## Current Status Summary

### Database Operations
- ✅ Simplified architecture with direct database integration
- ✅ 696+ total tests with 100% coverage
- ✅ Immediate and deferred execution patterns (TryGet vs TryRead)
- ✅ Centralized error handling via ResultReader
- ✅ Complete builder pattern implementation
- ✅ Registration-based factory with caching, defensive type checking, and named registrations
- ✅ Type signature validation support
- ✅ Comprehensive XML documentation on all interfaces and builder steps
- ✅ ConcurrentEntityCollection execution order (SetVersion before SetField)
- ✅ BackgroundExpirationUpdater last-write-wins semantics
- ✅ ReadResult proper encapsulation with private task field
- ✅ PriorityQueueScheduler sliding expiration with DateTime? LastAccess
- ✅ PriorityQueueScheduler testability improvements with time injection

### Database Size Collectors
- ✅ Production-ready keyspace monitoring
- ✅ 74 comprehensive tests
- ✅ INFO keyspace parsing with edge case handling
- ✅ Fluent DI registration
- ✅ Complete feature documentation

### Code Quality
- ✅ Consistent parameter validation across all classes
- ✅ Pure xUnit assertions (no FluentAssertions)
- ✅ Structured logging throughout
- ✅ Partial class organization for maintainability

### Documentation
- ✅ Root README for library overview
- ✅ Feature-specific READMEs (Database, DatabaseSizeCollectors)
- ✅ Complete XML documentation on builder interfaces
- ✅ CLAUDE.md with organized sections and TOC

---

## Usage Examples

### Database Operations - Collections Factory Registration

```csharp
// Startup/DI Configuration (runs once)
var factory = serviceProvider.GetRequiredService<ICollectionsFactory>();

// Register EntityCollection with standard serializer (default name)
factory.RegisterEntityCollection<int, User>(steps =>
    steps.WithKeySpace("users")
         .WithSerializer(jsonSerializer)
         .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
         .WithUniqueKeyFactory(id => id.ToString()));

// Register EntityCollection with signature validation (default name)
factory.RegisterEntityCollection<int, Product>(steps =>
    steps.WithKeySpace("products")
         .WithSignaturedSerializer(jsonSerializer)  // Automatic schema change detection
         .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(10))
         .WithUniqueKeyFactory(id => id.ToString()));

// Register multiple EntityCollections with different names for same type
factory.RegisterEntityCollection<int, User>(steps =>
    steps.WithKeySpace("users:cache")
         .WithSerializer(jsonSerializer)
         .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(60))
         .WithUniqueKeyFactory(id => id.ToString()), "long-cache");

factory.RegisterEntityCollection<int, User>(steps =>
    steps.WithKeySpace("users:session")
         .WithSerializer(jsonSerializer)
         .WithDefaultLifetimeProvider(TimeSpan.FromSeconds(30))
         .WithUniqueKeyFactory(id => id.ToString()), "session-cache");

// Register ConcurrentEntityCollection
factory.RegisterConcurrentEntityCollection<int, Order>(steps =>
    steps.WithKeySpace("orders")
         .WithSerializer(jsonSerializer)
         .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(10))
         .WithUniqueKeyFactory(id => id.ToString())
         .WithOldConcurrencyTokenSelector(order => order.Version)
         .WithNewConcurrencyTokenSelector(order => Guid.NewGuid().ToString()));

// Register ChildEntityCollection
factory.RegisterChildEntityCollection<int, string, OrderItem>(steps =>
    steps.WithKeySpace("order-items")
         .WithChildKeyPrefix("item")
         .WithSerializer(jsonSerializer)
         .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(10))
         .WithUniqueParentKeyFactory(orderId => orderId.ToString())
         .WithUniqueChildKeyFactory(itemId => itemId));

// Register StreamAdapter with custom name
factory.RegisterStreamAdapter<OrderEvent>(steps =>
    steps.WithStreamKey("orders:events")
         .WithSerializer(jsonSerializer)
         .WithMaxLength(10000), "order-events");

// Runtime Usage (fast, uses cached builders)
var context = new RedisContext(database);
var userCollection = factory.GetEntityCollection<int, User>(context); // default name
var longCacheUsers = factory.GetEntityCollection<int, User>(context, "long-cache");
var sessionUsers = factory.GetEntityCollection<int, User>(context, "session-cache");
var orderCollection = factory.GetConcurrentEntityCollection<int, Order>(context);
var orderEvents = factory.GetStreamAdapter<OrderEvent>(context, "order-events");
```

### Database Size Collectors - Basic Configuration

```csharp
// Register size collectors
builder.Services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)))
    .AddRedisSizeCollector(provider =>
        new RedisSizeCollectorSettings(
            database: provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(0),
            collectorName: "Redis-Primary",
            metrics: provider.GetRequiredService<IDatabaseMetricsConnector>()));
```

### Step Builder Pattern - Compile-Time Safety

```csharp
// ❌ This won't compile (missing steps)
factory.RegisterEntityCollection<int, User>(steps =>
    steps.WithKeySpace("users")
         .WithSerializer(serializer));
// Error: Cannot convert type 'ISerializerStep<int, User>' to 'IEntityCollectionBuilder<int, User>'

// ✅ Correct usage - all steps required
factory.RegisterEntityCollection<int, User>(steps =>
    steps.WithKeySpace("users")              // Returns ISerializerStep
         .WithSerializer(serializer)          // Returns ILifetimeProviderStep
         .WithDefaultLifetimeProvider(...)    // Returns IUniqueKeyFactoryStep
         .WithUniqueKeyFactory(...));         // Returns IEntityCollectionBuilder
```

### Workbench - Testing Different Scenarios

The Workbench project provides an interactive testing environment structured similarly to the Kafka Workbench:

**Project Structure:**
```
Workbench/
├── Program.cs                              # Entry point with scenario selection
├── StartupHostedService.cs                 # Hosted service for scenario lifecycle
├── Scenarios/
│   ├── IScenario.cs                        # Common interface for all scenarios
│   ├── Shared/                             # Shared types across scenarios
│   │   ├── Types.Person.cs                 # Person entity and PersonKey
│   │   ├── Types.Address.cs                # Address entity
│   │   └── JsonRedisSerializer.cs          # JSON serializer implementation
│   └── RedisDatabase/
│       ├── @Scenario.cs                    # Database operations scenario
│       └── RedisDBContext.cs               # Database context
```

**Running Scenarios:**
```bash
# Start the workbench
dotnet run --project Workbench

# When prompted, enter scenario name:
# - "redisdatabase"   - Test entity collections and database operations
```

**Scenario Implementation Pattern:**
Each scenario implements `IScenario` with:
- **Start()**: Launches async operations to test the feature
- **Stop()**: Cleanup operations (if needed)
- **Configure()**: Static method for DI registration and configuration

This pattern allows for easy addition of new scenarios and provides an isolated testing environment for each feature.
