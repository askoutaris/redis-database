# RedisDatabase

A comprehensive Redis integration library providing entity collections, caching, and data persistence with support for both immediate and deferred execution patterns.

## Overview

RedisDatabase is a .NET library that simplifies working with Redis by providing high-level abstractions for entity management, collections, and data persistence. It includes built-in support for expiration management, optimistic concurrency, hierarchical relationships, and Redis Streams.

### Features

- **Entity Collections** - Type-safe entity caching with automatic expiration management
- **Concurrent Collections** - Optimistic locking for conflict-free concurrent updates
- **Child/Parent Collections** - Hierarchical entity relationships
- **Redis Streams** - Event processing and stream operations
- **Flexible Execution** - Choose between immediate (TryGet) or deferred/batched (TryRead) execution
- **Background Expiration Updates** - Automatic TTL renewal for active entities
- **Pluggable Serialization** - Bring your own serializer implementation

## Installation

### Core Library
```bash
dotnet add package RedisDatabase
```

### Dependency Injection Extensions
```bash
dotnet add package RedisDatabase.Extensions.DependencyInjection
```

**Dependencies:**
- `StackExchange.Redis` (2.8+)
- `Microsoft.Extensions.Logging.Abstractions`

## Quick Start

### Basic Setup with Dependency Injection

```csharp
using RedisDatabase;
using RedisDatabase.Extensions.DependencyInjection;
using StackExchange.Redis;

// Register Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("localhost:6379"));

// Register RedisDatabase with collection configuration
builder.Services.AddRedisDatabase((provider, factory) =>
{
    var serializer = new JsonRedisSerializer();

    // Configure entity collections
    factory.RegisterEntityCollection<int, User>(steps => steps
        .WithKeySpace("users")
        .WithSerializer(serializer)
        .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(15))
        .WithUniqueKeyFactory(id => id.ToString()));

    // Configure concurrent collections with optimistic locking
    factory.RegisterConcurrentEntityCollection<int, Order>(steps => steps
        .WithKeySpace("orders")
        .WithSerializer(serializer)
        .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(30))
        .WithUniqueKeyFactory(id => id.ToString())
        .WithOldConcurrencyTokenSelector(order => order.Version)
        .WithNewConcurrencyTokenSelector(() => Guid.NewGuid().ToString()));
});
```

### Using Collections

```csharp
public class UserService
{
    private readonly IRedisCollectionsFactory _factory;
    private readonly IDatabase _database;

    public UserService(IRedisCollectionsFactory factory, IConnectionMultiplexer multiplexer)
    {
        _factory = factory;
        _database = multiplexer.GetDatabase();
    }

    public async Task<User?> GetUserAsync(int userId)
    {
        var context = new RedisContext(_database);
        var collection = _factory.GetEntityCollection<int, User>(context);

        // Immediate execution - single entity lookup
        return await collection.TryGet(userId);
    }

    public async Task SaveUserAsync(User user)
    {
        var context = new RedisContext(_database);
        var collection = _factory.GetEntityCollection<int, User>(context);

        await collection.Set(user.Id, user);
        await context.Commit();
    }
}
```

## Architecture

The library is organized into feature-specific namespaces:

```
RedisDatabase/
├── Collections/            # Entity, Concurrent, Child collections
├── Adapters/              # Low-level Redis adapters (String, Hash, Stream)
├── Builders/              # Fluent configuration builders
├── Serializers/           # Serialization infrastructure
├── Factories/             # Collection factory pattern
├── ExpirationUpdaters/    # Background TTL management
├── LifetimeProvider/      # Expiration policy management
└── PeriodicTriggers/      # Scheduled task infrastructure

RedisDatabase.Extensions.DependencyInjection/
└── ServiceCollectionExtensions  # DI registration helpers
```

## Key Concepts

### Serialization

The library uses a pluggable serialization strategy. You must provide your own `IRedisSerializer` implementation:

```csharp
public class JsonRedisSerializer : IRedisSerializer
{
    public byte[] Serialize<TType>(TType value) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));

    public TType? Deserialize<TType>(byte[] bytes) =>
        JsonSerializer.Deserialize<TType>(Encoding.UTF8.GetString(bytes));
}
```

### Connection Management

Always use a singleton `IConnectionMultiplexer`:

```csharp
// ✅ Correct - Singleton
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("localhost:6379"));

// ❌ Wrong - Don't create per request
```

## Execution Patterns

The library supports two execution patterns for read operations:

### Immediate Execution (TryGet)

**Use when:** You need a single entity immediately and want the simplest code.

**Characteristics:**
- Executes database operation immediately
- Returns `Task<TEntity?>` directly
- No batching - each call is a separate Redis command
- Ideal for single-entity lookups

**Example:**
```csharp
var context = new RedisContext(database);
var collection = factory.GetEntityCollection<int, User>(context);

// Immediate execution - returns Task<User?> directly
User? user = await collection.TryGet(userId);

if (user != null)
{
    Console.WriteLine($"Found user: {user.Name}");
}
```

**Adapter-level example:**
```csharp
var adapter = new StringAdapter(context, serializer, resultReader);

// Execute immediately without batching
User? user = await adapter.TryGet<User>("users:123");
```

### Deferred Execution (TryRead)

**Use when:** You need to batch multiple operations for optimal performance.

**Characteristics:**
- Registers operation in batch for later execution
- Returns `ReadResult<TEntity>` wrapper
- Operations execute together when batch is executed
- Ideal for reading multiple entities in parallel

**Example:**
```csharp
var context = new RedisContext(database);
var collection = factory.GetEntityCollection<int, User>(context);

// Deferred execution - returns ReadResult<User> wrapper
var userResult1 = collection.TryRead(userId1);
var userResult2 = collection.TryRead(userId2);
var userResult3 = collection.TryRead(userId3);

// Execute all operations in parallel
await context.ExecuteBatch();

// Access results
User? user1 = await userResult1;
User? user2 = await userResult2;
User? user3 = await userResult3;
```

**Adapter-level example:**
```csharp
var adapter = new HashsetAdapter(context, serializer, resultReader);

// Register multiple operations in batch
var result1 = adapter.TryReadField<User>("users:hash", "user:1");
var result2 = adapter.TryReadField<User>("users:hash", "user:2");

// Execute batch
await context.ExecuteBatch();

// Access results
User? user1 = await result1;
User? user2 = await result2;
```

### Choosing Between Patterns

| Scenario | Pattern | Method |
|----------|---------|--------|
| Single entity lookup | Immediate | `TryGet` |
| Multiple entities from different keys | Deferred | `TryRead` + `ExecuteBatch` |
| Simple, straightforward code | Immediate | `TryGet` |
| Performance-critical batch operations | Deferred | `TryRead` + `ExecuteBatch` |

### All Collection Types Support Both Patterns

```csharp
// EntityCollection
var user = await entityCollection.TryGet(userId);                    // Immediate
var userResult = entityCollection.TryRead(userId);                   // Deferred

// ConcurrentEntityCollection
var order = await concurrentCollection.TryGet(orderId);              // Immediate
var orderResult = concurrentCollection.TryRead(orderId);             // Deferred

// ChildEntityCollection
var item = await childCollection.TryGetChild(orderId, itemId);       // Immediate
var itemResult = childCollection.TryReadChild(orderId, itemId);      // Deferred
```

## Documentation

- **[CLAUDE.md](./CLAUDE.md)** - Detailed technical architecture and development documentation (for contributors)

## Best Practices

1. **Use Singleton Connections** - `IConnectionMultiplexer` should be registered as a singleton
2. **Bring Your Own Serializer** - Implement `IRedisSerializer` for your serialization needs (JSON, MessagePack, etc.)
3. **Choose the Right Execution Pattern**:
   - Use `TryGet` for single entity lookups (immediate execution)
   - Use `TryRead` + `ExecuteBatch` for multiple operations (deferred execution)
4. **Batch Operations** - Use `RedisContext` batching for multiple operations to minimize network round-trips
5. **Handle Conflicts** - Catch `RedisConflictException` when using concurrent collections with optimistic locking
6. **Configure Expiration** - Set appropriate TTL values using `WithDefaultLifetimeProvider` to prevent memory bloat
7. **Register Collections at Startup** - Register all collections during application startup for best performance

## Named Registrations

You can register multiple collections of the same type with different configurations using the optional `name` parameter. This is useful for having different caching strategies or TTLs for the same entity type.

```csharp
builder.Services.AddRedisDatabase((provider, factory) =>
{
    var serializer = new JsonRedisSerializer();

    // Short-lived cache
    factory.RegisterEntityCollection<int, User>(
        steps => steps
            .WithKeySpace("users:cache")
            .WithSerializer(serializer)
            .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(5))
            .WithUniqueKeyFactory(id => id.ToString()),
        name: "short-cache");

    // Long-lived cache
    factory.RegisterEntityCollection<int, User>(
        steps => steps
            .WithKeySpace("users:persistent")
            .WithSerializer(serializer)
            .WithDefaultLifetimeProvider(TimeSpan.FromHours(1))
            .WithUniqueKeyFactory(id => id.ToString()),
        name: "long-cache");
});

// Retrieving named collections
var shortCache = factory.GetEntityCollection<int, User>(context, "short-cache");
var longCache = factory.GetEntityCollection<int, User>(context, "long-cache");
```

**Note:** If you don't specify a name, it defaults to `"default"`.

## Collection Types

### EntityCollection
Basic entity storage with automatic expiration management. Ideal for simple caching scenarios.

```csharp
factory.RegisterEntityCollection<int, User>(steps => steps
    .WithKeySpace("users")
    .WithSerializer(serializer)
    .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(15))
    .WithUniqueKeyFactory(id => id.ToString()));
```

### ConcurrentEntityCollection
Entity storage with optimistic locking for handling concurrent updates without conflicts.

```csharp
factory.RegisterConcurrentEntityCollection<int, Order>(steps => steps
    .WithKeySpace("orders")
    .WithSerializer(serializer)
    .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(30))
    .WithUniqueKeyFactory(id => id.ToString())
    .WithOldConcurrencyTokenSelector(order => order.Version)  // Return null for new items!
    .WithNewConcurrencyTokenSelector(() => Guid.NewGuid().ToString()));
```

#### How Optimistic Concurrency Works

The concurrent collection uses version tokens to prevent conflicting updates:

1. **WithOldConcurrencyTokenSelector**: Extracts the current version from the entity
   - **For new items**: Must return `null` (item doesn't exist in Redis yet)
   - **For existing items**: Returns the current version (e.g., `order.Version`)

2. **WithNewConcurrencyTokenSelector**: Generates a new version token when saving
   - Called every time you save an item
   - Common implementations: `Guid.NewGuid().ToString()`, incrementing integers, timestamps

**Example Entity:**
```csharp
public class Order
{
    public int Id { get; set; }
    public string? Version { get; set; }  // Null for new orders
    public decimal Total { get; set; }
    // ... other properties
}

// Creating a new order
var newOrder = new Order
{
    Id = 123,
    Version = null,  // ⚠️ IMPORTANT: null indicates this is a new item
    Total = 99.99m
};

// Updating an existing order
var existingOrder = await collection.TryGet(123);
existingOrder.Total = 149.99m;
// Version is still "abc-123" - Redis will verify this matches before updating
```

**What Happens During Save:**
```csharp
await collection.Set(order.Id, order);
await context.Commit();

// Internally:
// 1. Reads order.Version (null for new, "abc-123" for existing)
// 2. Checks Redis: if Version is null, key must NOT exist; if "abc-123", key must have that version
// 3. If check passes: saves with new version from WithNewConcurrencyTokenSelector
// 4. If check fails: throws RedisConflictException
```

**Handling Conflicts:**
```csharp
try
{
    var order = await collection.TryGet(orderId);
    order.Total = 149.99m;

    await collection.Set(orderId, order);
    await context.Commit();
}
catch (RedisConflictException)
{
    // Another process updated this order - retry or handle appropriately
    // Common strategies: retry with exponential backoff, merge changes, notify user
}
```

### ChildEntityCollection
Hierarchical entity relationships where child entities belong to a parent entity.

```csharp
factory.RegisterChildEntityCollection<int, string, OrderItem>(steps => steps
    .WithKeySpace("orders")
    .WithChildKeyPrefix("items")
    .WithSerializer(serializer)
    .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(30))
    .WithUniqueParentKeyFactory(orderId => orderId.ToString())
    .WithUniqueChildKeyFactory(itemId => itemId));
```

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

See [LICENSE](./LICENSE) file for details.
