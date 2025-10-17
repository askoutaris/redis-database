# BlueBrown.Sportsbook.Redis

Comprehensive Redis integration library for BlueBrown Sportsbook applications.

## Overview

This library provides Redis functionality for the BlueBrown.Sportsbook platform, offering multiple features for different use cases:

### Features

- **[Database Operations](./Database/README.md)** - Entity caching, collections, streams, and data persistence
  - Entity Collections with expiration management
  - Concurrent Collections with optimistic locking
  - Child/Parent hierarchical relationships
  - Redis Streams for event processing
  - **Immediate execution** (TryGet) - Direct database operations without batching
  - **Deferred execution** (TryRead) - Batched operations for optimal performance

- **[Database Size Monitoring](./DatabaseSizeCollectors/README.md)** - Redis keyspace metrics collection
  - Keyspace statistics tracking
  - Integration with Common metrics connector
  - Scheduled background collection

## Installation

```bash
dotnet add package BlueBrown.Sportsbook.Redis
```

**Dependencies:**
- `BlueBrown.Sportsbook.Common`
- `StackExchange.Redis`

## Quick Start

### Database Operations

For entity caching, collections, and Redis operations:

```csharp
using BlueBrown.Sportsbook.Redis.Database;
using StackExchange.Redis;

// Register Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("localhost:6379"));

// Register database functionality
builder.Services.AddRedisDatabase((provider, factory) =>
{
    var serializer = new JsonRedisSerializer();

    // Configure your collections
    factory.RegisterEntityCollection<int, User>(cfg => cfg
        .WithKeySpace("users")
        .WithSerializer(serializer)
        .WithDefaultLifetimeProvider(TimeSpan.FromMinutes(15))
        .WithUniqueKeyFactory(id => id.ToString()));
});
```

See [Database README](./Database/README.md) for detailed documentation.

### Size Monitoring

For monitoring Redis keyspace metrics:

```csharp
using BlueBrown.Sportsbook.Redis.DatabaseSizeCollectors;

builder.Services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)))
    .AddRedisSizeCollector(provider =>
        new RedisSizeCollectorSettings(
            database: provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(0),
            collectorName: "Redis-Cache",
            metrics: provider.GetRequiredService<IDatabaseMetricsConnector>()));
```

## Architecture

The library is organized into feature-specific namespaces:

```
BlueBrown.Sportsbook.Redis
├── Database/               # Entity collections, streams, caching
│   ├── Collections/        # Entity, Concurrent, Child collections
│   ├── Adapters/          # Low-level Redis adapters
│   ├── Builders/          # Fluent configuration builders
│   ├── Serializers/       # Serialization infrastructure
│   └── Factories/         # Collection factory pattern
└── DatabaseSizeCollectors/ # Keyspace monitoring
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

## Feature Documentation

- **[Database Operations](./Database/README.md)** - Complete guide to entity collections, streams, and caching
- **[Database Size Monitoring](./DatabaseSizeCollectors/README.md)** - Redis keyspace metrics collection and monitoring
- **[CLAUDE.md](./CLAUDE.md)** - Detailed technical architecture and development documentation

## Best Practices

1. **Use Singleton Connections** - `IConnectionMultiplexer` should be registered as a singleton
2. **Bring Your Own Serializer** - Implement `IRedisSerializer` for your use case
3. **Choose the Right Execution Pattern**:
   - Use `TryGet` for single entity lookups (immediate execution)
   - Use `TryRead` + `ExecuteBatch` for multiple operations (deferred execution)
4. **Batch Operations** - Use `RedisContext` batching for multiple operations to minimize network round-trips
5. **Monitor Keyspace** - Use size collectors to track Redis memory usage
6. **Handle Conflicts** - Catch `RedisConflictException` for concurrent operations

## Support

For issues, questions, or contributions, please refer to the project repository or contact the BlueBrown Development Team.
