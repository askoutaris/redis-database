# BlueBrown.Sportsbook.Common

Core infrastructure library providing database size monitoring capabilities for BlueBrown Sportsbook services.

## Overview

This is the foundational package that provides the core infrastructure for database size monitoring across different database types. It includes a hosted service architecture that can collect database size metrics at regular intervals and report them through a pluggable metrics system.

## Installation

```bash
dotnet add package BlueBrown.Sportsbook.Common
```

## Key Features

- **Hosted Service Architecture** - Background service that runs database size collection at configurable intervals
- **Pluggable Collectors** - Support for multiple database types through extension packages
- **Metrics Integration** - Configurable metrics reporting through `IDatabaseMetricsConnector`
- **Fluent Configuration** - Easy-to-use fluent API for service registration
- **Dependency Injection** - Full DI container integration
- **Extensible Design** - Easy to add new database types and collectors

## Core Components

### Interfaces

#### `IDatabaseSizeCollector`
Base interface for all database size collectors.

```csharp
public interface IDatabaseSizeCollector
{
    Task Collect();
}
```

#### `IDatabaseMetricsConnector`
Interface for reporting collected metrics.

```csharp
public interface IDatabaseMetricsConnector
{
    void RecordSize(string collectorName, string collectionName, long count);
}
```

#### `IDatabaseSizeCollectorsRegistry`
Registry interface for fluent configuration.

```csharp
public interface IDatabaseSizeCollectorsRegistry
{
    IServiceCollection Services { get; }
}
```

### Configuration

#### `DatabaseSizeCollectorsSettings`
Configuration settings for the monitoring system.

```csharp
public class DatabaseSizeCollectorsSettings
{
    public DatabaseSizeCollectorsSettings(TimeSpan period);
    public TimeSpan Period { get; }
}
```

## Basic Usage

### 1. Implement Metrics Connector

First, implement the `IDatabaseMetricsConnector` interface to define how metrics are reported:

```csharp
public class MyMetricsConnector : IDatabaseMetricsConnector
{
    private readonly ILogger<MyMetricsConnector> _logger;

    public MyMetricsConnector(ILogger<MyMetricsConnector> logger)
    {
        _logger = logger;
    }

    public void RecordSize(string collectorName, string collectionName, long count)
    {
        _logger.LogInformation("Collector {CollectorName}: {CollectionName} = {Count}",
            collectorName, collectionName, count);

        // Report to your metrics system (Prometheus, Application Insights, etc.)
        // Example: _metricsClient.ReportGauge("database_collection_size", count,
        //     new[] { ("collector", collectorName), ("collection", collectionName) });
    }
}
```

### 2. Configure Services

Register the core services in your DI container:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register your metrics connector
builder.Services.AddSingleton<IDatabaseMetricsConnector, MyMetricsConnector>();

// Configure database size collectors (runs every 5 minutes)
builder.Services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)));

var app = builder.Build();
app.Run();
```

### 3. Add Database-Specific Collectors

Use extension packages to add specific database types:

```csharp
// Add SQL Server monitoring
builder.Services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)))
    .AddSqlSizeCollector(provider =>
        new SqlSizeCollectorSettings(
            connectionString: "Server=localhost;Database=MyApp;Trusted_Connection=true;",
            collectorName: "MainDatabase",
            metrics: provider.GetRequiredService<IDatabaseMetricsConnector>()));
```

## Advanced Configuration

### Multiple Monitoring Intervals

You can only configure one monitoring interval per application, but you can add multiple collectors:

```csharp
builder.Services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(10)))
    .AddSqlSizeCollector(provider => /* SQL config */)
    .AddRedisSizeCollector(provider => /* Redis config */);
```

### Custom Collectors

Implement your own database collector:

```csharp
public class CustomDatabaseCollector : IDatabaseSizeCollector
{
    private readonly string _collectorName;
    private readonly IDatabaseMetricsConnector _metrics;

    public CustomDatabaseCollector(string collectorName, IDatabaseMetricsConnector metrics)
    {
        _collectorName = collectorName;
        _metrics = metrics;
    }

    public Task Collect()
    {
        // Your custom collection logic here
        var tableCount = GetTableCount();
        var recordCount = GetRecordCount();

        // Record different metrics
        _metrics.RecordSize(_collectorName, "Tables", tableCount);
        _metrics.RecordSize(_collectorName, "Records", recordCount);

        return Task.CompletedTask;
    }

    private long GetTableCount()
    {
        // Implement your collection logic
        return 5; // Example: 5 tables
    }

    private long GetRecordCount()
    {
        // Implement your collection logic
        return 10000; // Example: 10,000 records
    }
}

// Register your custom collector
builder.Services.AddDatabaseSizeCollectors(provider =>
    new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(5)));

builder.Services.AddSingleton<IDatabaseSizeCollector>(provider =>
    new CustomDatabaseCollector(
        "MyCustomDB",
        provider.GetRequiredService<IDatabaseMetricsConnector>()));
```

## Extension Packages

The Common library is designed to work with database-specific extension packages:

### Available Extensions

- **[BlueBrown.Sportsbook.SQL](../SQL/README.md)** - SQL Server database monitoring
- **[BlueBrown.Sportsbook.Redis](../Redis/README.md)** - Redis memory and keyspace monitoring

### Creating Custom Extensions

To create your own extension package:

1. Reference `BlueBrown.Sportsbook.Common`
2. Implement `IDatabaseSizeCollector` for your database type
3. Create extension methods for `IDatabaseSizeCollectorsRegistry`
4. Package and distribute

Example extension method:

```csharp
public static class MyDatabaseExtensions
{
    public static IDatabaseSizeCollectorsRegistry AddMyDatabaseCollector(
        this IDatabaseSizeCollectorsRegistry registry,
        Func<IServiceProvider, MyDatabaseSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(registry);

        registry.Services.AddSingleton<IDatabaseSizeCollector>(ctx =>
        {
            var settings = configure(ctx);
            return new MyDatabaseCollector(settings);
        });

        return registry;
    }
}
```

## Architecture

### Hosted Service Pattern

The library uses .NET's `IHostedService` pattern to run background collection:

```
Application Startup
       ↓
DatabaseSizeCollectorsRegistry
       ↓
HostedService (Background Service)
       ↓
Periodic Collection (every Period)
       ↓
For Each IDatabaseSizeCollector:
       ↓
collector.Collect()
       ↓
IDatabaseMetricsConnector.RecordSize()
```

### Dependency Injection Flow

1. **Registration Phase**: Services and collectors are registered with DI container
2. **Resolution Phase**: Hosted service resolves all `IDatabaseSizeCollector` instances
3. **Execution Phase**: Background service calls each collector at configured intervals

## Configuration Patterns

### Environment-Specific Settings

```csharp
builder.Services.AddDatabaseSizeCollectors(provider =>
{
    var environment = provider.GetRequiredService<IWebHostEnvironment>();
    var interval = environment.IsDevelopment()
        ? TimeSpan.FromMinutes(1)  // Frequent in dev
        : TimeSpan.FromMinutes(15); // Less frequent in prod

    return new DatabaseSizeCollectorsSettings(interval);
});
```

### Configuration-Based Setup

```csharp
// appsettings.json
{
  "DatabaseMonitoring": {
    "IntervalMinutes": 10,
    "Enabled": true
  }
}

// In code
var config = builder.Configuration.GetSection("DatabaseMonitoring");
if (config.GetValue<bool>("Enabled"))
{
    var intervalMinutes = config.GetValue<int>("IntervalMinutes", 5);
    builder.Services.AddDatabaseSizeCollectors(provider =>
        new DatabaseSizeCollectorsSettings(TimeSpan.FromMinutes(intervalMinutes)));
}
```

## Logging and Diagnostics

The library integrates with .NET logging:

```csharp
// Enable debug logging for database collectors
builder.Logging.AddFilter("BlueBrown.Sportsbook.Common.Infrastructure.DatabaseSizeCollectors", LogLevel.Debug);
```

Logs include:
- Collection start/completion
- Individual collector execution
- Error handling and retries
- Performance metrics

## Error Handling

The hosted service includes robust error handling:

- **Individual Collector Failures**: One collector failure doesn't stop others
- **Retry Logic**: Automatic retry for transient failures
- **Graceful Degradation**: Service continues running despite errors
- **Comprehensive Logging**: All errors are logged with context

## Performance Considerations

- **Lightweight Collections**: Database queries are optimized for minimal impact
- **Parallel Execution**: Multiple collectors can run concurrently
- **Resource Management**: Proper disposal of database connections
- **Configurable Intervals**: Adjust frequency based on your needs

## Testing

### Unit Testing Collectors

```csharp
[Test]
public async Task CustomCollector_ShouldReportMetrics()
{
    // Arrange
    var mockMetrics = Substitute.For<IDatabaseMetricsConnector>();
    var collector = new CustomDatabaseCollector("test", mockMetrics);

    // Act
    await collector.Collect();

    // Assert
    mockMetrics.Received(1).RecordSize("test", Arg.Any<string>(), Arg.Any<long>());
}
```

### Integration Testing

```csharp
[Test]
public async Task HostedService_ShouldCollectFromAllRegisteredCollectors()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<IDatabaseMetricsConnector, TestMetricsConnector>();
    services.AddDatabaseSizeCollectors(provider =>
        new DatabaseSizeCollectorsSettings(TimeSpan.FromMilliseconds(100)));

    var serviceProvider = services.BuildServiceProvider();
    var hostedService = serviceProvider.GetRequiredService<IHostedService>();

    // Act & Assert
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    await hostedService.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token); // Let it run
    await hostedService.StopAsync(cts.Token);
}
```

## Best Practices

1. **Metrics Connector**: Implement robust error handling in your metrics connector
2. **Database Permissions**: Use least-privilege accounts for database connections
3. **Monitoring Intervals**: Choose intervals based on your database change frequency
4. **Resource Management**: Ensure proper disposal of database connections
5. **Error Handling**: Log errors but don't let them crash the application
6. **Testing**: Test both individual collectors and the complete integration

## Troubleshooting

### Common Issues

1. **Service Not Starting**
   - Check that `IDatabaseMetricsConnector` is registered
   - Verify logging configuration
   - Review startup errors

2. **Collectors Not Running**
   - Confirm collectors are registered with DI
   - Check service registration order
   - Review hosted service logs

3. **Metrics Not Appearing**
   - Verify metrics connector implementation
   - Check network connectivity to metrics system
   - Review error logs

### Debug Logging

Enable detailed logging to troubleshoot issues:

```csharp
builder.Logging.AddConsole()
       .AddFilter("BlueBrown.Sportsbook.Common", LogLevel.Debug);
```

## Related Documentation

- [SQL Extension Package](../SQL/README.md)
- [Redis Extension Package](../Redis/README.md)
- [Architecture Documentation](CLAUDE.md)

## Support

For issues, questions, or contributions, please refer to the project repository or contact the BlueBrown Development Team.