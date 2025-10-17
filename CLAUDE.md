# Claude Context - Core.Sportsbook.Common

## Project Overview
This is a .NET library containing common infrastructure components for the BlueBrown Sportsbook application, specifically focusing on database size collection and monitoring capabilities.

## Key Components

### PeriodicWork Infrastructure
Located in `Threading\PeriodicWork.cs` - A reusable infrastructure for executing periodic tasks with proper resource management and testability.

#### Core Components
- **AsyncEventHandler**: Delegate for asynchronous event handlers (`public delegate Task AsyncEventHandler()`)
- **IPeriodicWork**: Interface defining periodic work contracts with event-driven architecture
- **PeriodicWork**: Implementation providing timer-based periodic execution with proper disposal patterns

#### Key Features
- **Event-Driven Architecture**: Uses `OnTick` event for decoupled periodic task execution
- **Proper Resource Management**: Implements `IDisposable` with field-level `PeriodicTimer` management
- **Testable Design**: Interface-based design allows easy mocking and dependency injection
- **Cancellation Support**: Handles `CancellationToken` for graceful shutdown
- **Exception Handling**: Catches and logs exceptions without stopping the periodic execution
- **Clean Lifecycle**: `Stop()` disposes timer, preventing restart (one-time use pattern)

#### Architecture Decisions
- **Field-Level Timer**: `PeriodicTimer` moved from local to field level for proper disposal control
- **Stop() Behavior**: Calling `Stop()` disposes the timer, causing normal loop exit (no restart capability)
- **Simple Event Model**: Single `OnTick` event for maximum flexibility and testability
- **No Restart After Stop**: Once stopped, the instance cannot be restarted (create new instance)

### Database Size Collectors
Located in `Infrastructure\DatabaseSizeCollectors\` - A system for periodically collecting and reporting database size metrics using a centralized hosted service architecture.

#### Core Interfaces
- **IDatabaseSizeCollector**: Contract for database size collectors that gather metrics
- **IDatabaseMetricsConnector**: Contract for reporting collected metrics to monitoring systems
- **IDatabaseSizeCollectorsRegistry**: Fluent interface for registering multiple collectors

#### Implementations
- **HostedService**: Non-generic background service that manages multiple collectors simultaneously
- Extension packages provide specific collectors:
  - **SqlSizeCollector**: (in SQL package) Collects table row counts from SQL Server using system views
  - **RedisSizeCollector**: (in Redis package) Collects keyspace metrics from Redis using INFO KEYSPACE command

#### Configuration
- **DatabaseSizeCollectorsSettings**: Central timing configuration for the hosted service (period setting)
- **DatabaseSizeCollectorsRegistry**: Internal registry implementation for fluent collector registration
- Extension package settings:
  - **SqlSizeCollectorSettings**: (in SQL package) Configuration for SQL Server collectors
  - **RedisSizeCollectorSettings**: (in Redis package) Configuration for Redis collectors

#### Service Registration
- **ServiceCollectionExtensions**: Extension methods to register the database size collection system
  - `AddDatabaseSizeCollectors()`: Registers the central hosted service and returns a registry for adding collectors
- Extension package registration methods:
  - `AddSqlSizeCollector()`: (in SQL package) Registers SQL Server collectors
  - `AddRedisSizeCollector()`: (in Redis package) Registers Redis collectors

## Architecture Patterns

### Centralized Hosted Service Pattern (Current Architecture)
The system uses a single `HostedService` that:
- Manages multiple collectors of different types in one service
- Runs as a background service in .NET applications
- Executes all registered collectors at the same configurable interval (default: 30 seconds)
- Handles cancellation tokens gracefully for all collectors
- Provides comprehensive error logging with per-collector exception isolation
- Uses `IReadOnlyCollection<IDatabaseSizeCollector>` resolved from DI to get all registered collectors

### Registry Pattern
The fluent registration pattern:
```csharp
services.AddDatabaseSizeCollectors(provider => new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)))
    .AddSqlSizeCollector(provider => new SqlSizeCollectorSettings(...))
    .AddRedisSizeCollector(provider => new RedisSizeCollectorSettings(...));
```

### Settings Pattern
Configuration classes follow a consistent pattern:
- Required dependencies as constructor parameters (connection info, collector name, metrics connector)
- Immutable properties
- Simple constructor with all required parameters
- No period settings (timing controlled centrally by DatabaseSizeCollectorsSettings)

## Service Registration Behavior

### Important Registration Rules
1. **Single Hosted Service**: Only one `HostedService` instance is registered per application
2. **First Configuration Wins**: Multiple calls to `AddDatabaseSizeCollectors()` keep the first settings (subsequent calls are ignored)
3. **Collector Collection**: The hosted service automatically resolves all registered `IDatabaseSizeCollector` instances via DI
4. **Empty Collections Supported**: The system works with zero collectors (empty collection)

### Registration Flow
1. `AddDatabaseSizeCollectors()` registers the `HostedService` with the specified timing settings
2. Registry methods from extension packages register individual collectors as singletons implementing `IDatabaseSizeCollector`
3. At runtime, the `HostedService` resolves all collectors via `IServiceProvider.GetServices<IDatabaseSizeCollector>()`

## Testing Strategy

### Test Structure
Common tests are in `Tests\CommonTests\`:

#### Threading Infrastructure Tests
- **PeriodicWorkTests.cs**: Comprehensive testing of PeriodicWork implementation with 17 test methods covering:
  - Constructor behavior and interface implementation
  - Event subscription and multiple subscriber scenarios
  - Exception handling and continuation after errors
  - Cancellation token support and timing behavior
  - Start/Stop lifecycle and resource disposal
  - Timer disposal behavior and restart prevention
  - Timing accuracy verification with tolerance for system variations

#### Database Size Collectors Tests
Located in `Infrastructure\DatabaseSizeCollectors\`:
- **HostedServiceTests.cs**: Non-generic hosted service testing with multiple collectors and timing scenarios
- **DatabaseSizeCollectorsRegistryTests.cs**: Registry pattern and fluent interface testing
- **ServiceCollectionExtensionsTests.cs**: Service registration, dependency injection, and integration testing
- **DatabaseSizeCollectorsSettingsTests.cs**: Settings class validation and immutability testing
- **TestLogger.cs**: Shared test logger implementation for unit tests

Extension package tests are in separate projects:
- `Tests\SQLTests\` - SQL Server collector tests
- `Tests\RedisTests\` - Redis collector tests

### Key Testing Challenges
1. **Timing-Dependent Tests**: PeriodicWork and background services require careful synchronization and tolerance handling
2. **Internal Classes**: Many classes are internal, requiring reflection for testing
3. **Collection-based Architecture**: Tests must handle multiple collectors and their interactions
4. **Registry Pattern Testing**: Fluent interface and service registration validation
5. **Event-Based Architecture**: Testing event subscription, multiple subscribers, and event timing
6. **Resource Disposal**: Testing proper disposal patterns and lifecycle management
7. **Database Connections**: Tests handle both successful and failed connection scenarios
8. **Service Registration Behavior**: Testing "first wins" behavior and empty collection scenarios

### Mock Strategy
- **NSubstitute** for interface mocking
- **TestLogger<T>** implementations instead of mocking ILogger (due to internal class constraints)
- **Reflection** for testing internal class behavior and private fields
- **TaskCompletionSource** for synchronization in timing-sensitive tests
- **Multiple Collector Scenarios** for testing collection-based execution

## Code Coverage Notes

### Challenging Areas
- **HostedService execution**: Testing multiple collectors and error isolation
- **Registry fluent interface**: Ensuring proper service registration patterns
- **Settings validation**: Testing immutability and constructor parameter validation
- **Service registration behavior**: Testing "first wins" behavior and empty collection scenarios

### Coverage Strategy
- Use mock collectors for testing hosted service behavior
- Test empty collector collections and multiple collector scenarios
- Design tests to cover both success and failure paths
- Use reflection to verify private field initialization
- Test service registration edge cases (multiple calls, empty registries)

### Testing Best Practices
- **Avoid Over-Engineering**: Don't create excessive tests for simple classes. A basic immutable data class with 3 properties doesn't need 25+ tests
- **Focus on Core Functionality**: Write comprehensive tests for complex business logic, algorithms, and state management
- **Essential Coverage Only**: For simple classes, test main functionality with various parameters, null/edge cases, and boundary conditions
- **Quality over Quantity**: 8 meaningful tests are better than 25 redundant tests that test obvious behaviors
- **Practical Testing**: Test what matters - constructor parameter handling, core methods, error scenarios. Skip testing compiler-enforced behaviors like readonly properties
- **Use Standard xUnit Assertions**: Use standard xUnit assertions like `Assert.NotNull()`, `Assert.Equal()`, `Assert.Throws<T>()` instead of FluentAssertions library for consistency and simplicity

## Dependencies

### Core Dependencies
- **Microsoft.Extensions.Hosting**: Background service support
- **Microsoft.Extensions.DependencyInjection**: Service registration
- **Microsoft.Extensions.Logging**: Logging infrastructure

### Extension Package Dependencies
- **Microsoft.Data.SqlClient**: (SQL package) SQL Server connectivity
- **StackExchange.Redis**: (Redis package) Redis connectivity

### Test Dependencies
- **xUnit**: Testing framework with standard assertions (`Assert.Equal()`, `Assert.NotNull()`, etc.)
- **NSubstitute**: Mocking framework
- **Note**: FluentAssertions is not used - prefer standard xUnit assertions for consistency

## Build and Test Commands
- **Build**: Use standard .NET build commands
- **Tests**: Run with standard test runners
- **Code Coverage**: Tests are designed for comprehensive coverage of all execution paths
- **Current Status**: All tests passing across all projects ✅

## Common Issues and Solutions

### NSubstitute Limitations
- Cannot mock internal classes - use TestLogger implementations
- Cannot create proxies for `ILogger<InternalClass>` - use TestLogger<T> instead
- Be careful with generic type constraints in mock setup
- Use `Received()` instead of complex argument matchers for reliability

### Service Registration Gotchas
- Multiple calls to `AddDatabaseSizeCollectors()` keep first configuration only
- `AddHostedService()` doesn't allow multiple registrations of same implementation type
- Must use `GetServices<IDatabaseSizeCollector>()` not `GetRequiredService<IReadOnlyCollection<IDatabaseSizeCollector>>()`
- Empty collector collections are valid and handled gracefully

### Timing Test Reliability
- Use TaskCompletionSource for coordination instead of Task.Delay
- Design tests to be deterministic rather than timing-dependent
- Accept race conditions in cancellation scenarios where timing is inherently unreliable
- Properly dispose CancellationTokenSource to avoid ObjectDisposedException

### Extension Package Testing
- Database connection tests are in respective extension packages
- Common package focuses on infrastructure and service registration testing
- Mock implementations used for testing hosted service behavior

## Core Package Features

The Common package provides:

### Threading Infrastructure
- **PeriodicWork**: Reusable periodic task execution with event-driven architecture
- **IPeriodicWork**: Interface for testable periodic work implementations
- **AsyncEventHandler**: Delegate for asynchronous event handling
- Proper resource management with IDisposable pattern
- Exception handling and logging for robustness

### Database Size Collection Infrastructure
- Core interfaces (`IDatabaseSizeCollector`, `IDatabaseMetricsConnector`, `IDatabaseSizeCollectorsRegistry`)
- Hosted service infrastructure for background collection
- Service registration and dependency injection support
- Fluent configuration API
- Centralized timing configuration
- Comprehensive error handling and logging

## Usage Examples

### PeriodicWork Usage
```csharp
// Basic usage
var logger = serviceProvider.GetRequiredService<ILogger<PeriodicWork>>();
using var periodicWork = new PeriodicWork(TimeSpan.FromSeconds(5), logger);

// Subscribe to periodic events
periodicWork.OnTick += async () =>
{
    Console.WriteLine($"Periodic task executed at {DateTime.Now}");
    await SomeAsyncWork();
};

// Start the periodic execution
using var cts = new CancellationTokenSource();
periodicWork.Start(cts.Token);

// Stop when needed (disposes timer, prevents restart)
await periodicWork.Stop();
```

### Dependency Injection with PeriodicWork
```csharp
// Register in DI container
services.AddSingleton<IPeriodicWork>(provider =>
    new PeriodicWork(TimeSpan.FromMinutes(1), provider.GetRequiredService<ILogger<PeriodicWork>>()));

// Use in a service
public class MyBackgroundService : IHostedService
{
    private readonly IPeriodicWork _periodicWork;

    public MyBackgroundService(IPeriodicWork periodicWork)
    {
        _periodicWork = periodicWork;
        _periodicWork.OnTick += DoPeriodicWork;
    }

    private async Task DoPeriodicWork()
    {
        // Your periodic logic here
    }
}
```

### Database Size Collectors Registration
```csharp
// Common package provides core infrastructure
services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)));

// Extension packages add specific collectors
// (requires SQL and Redis package references)
// .AddSqlSizeCollector(provider => new SqlSizeCollectorSettings(...))
// .AddRedisSizeCollector(provider => new RedisSizeCollectorSettings(...))
```

### Implementing IDatabaseMetricsConnector
```csharp
public class MyMetricsConnector : IDatabaseMetricsConnector
{
    public void RecordSize(string collectorName, string collectionName, long count)
    {
        // Log or send to monitoring system
        Console.WriteLine($"{collectorName}: {collectionName} = {count}");
    }
}

// Register it
services.AddSingleton<IDatabaseMetricsConnector, MyMetricsConnector>();
```

## Extension Packages

### Available Extensions
- **BlueBrown.Sportsbook.SQL**: SQL Server table row count monitoring
- **BlueBrown.Sportsbook.Redis**: Redis keyspace monitoring

### Package Structure
- **Common**: Core infrastructure, interfaces, hosted service, registry pattern
- **SQL**: SQL Server-specific collector implementation and configuration
- **Redis**: Redis-specific collector implementation and configuration
- **Tests**: Separate test projects for each package

## Recent Work History

### PeriodicWork Infrastructure Development (2024)
- **Initial Implementation**: Created event-based periodic execution infrastructure with `IPeriodicWork` interface
- **Testability Refactor**: Moved from local `PeriodicTimer` to field-level for proper resource management
- **IDisposable Implementation**: Added proper disposal pattern with `GC.SuppressFinalize(this)`
- **Stop() Behavior Update**: `Stop()` now disposes timer, preventing restart and allowing normal loop exit
- **Complete Test Coverage**: 17 comprehensive unit tests covering all scenarios including timing, disposal, and edge cases
- **Coverage Issue Resolution**: Fixed unreachable code issue (line 47) through architectural improvements
- **Documentation**: Comprehensive XML documentation and usage examples

### Database Size Collectors Evolution
- **Major Architecture Refactor**: Changed from generic `HostedService<TCollector>` to single `HostedService` managing multiple collectors
- **Registry Pattern Implementation**: Added fluent interface for collector registration
- **Centralized Settings**: Introduced `DatabaseSizeCollectorsSettings` for timing configuration
- **Extension Package Structure**: Separated SQL and Redis implementations into extension packages
- **Complete Test Suite**: Comprehensive test coverage across all packages with proper separation
- **Service Registration Fixes**: Fixed DI resolution to use `GetServices()` instead of requiring pre-registered collections
- **Comprehensive Documentation**: Updated all class summaries, XML documentation, and README files
- **Code Cleanup**: Removed redundant code and improved line coverage