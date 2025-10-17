# Redis Workbench Docker Setup

This docker-compose configuration provides Redis and Redis Insight for development and testing.

## Services

### Redis
- **Image**: `redis:7-alpine` (latest stable Redis 7.x)
- **Port**: 6379 (default Redis port)
- **Persistence**: Enabled with AOF (Append Only File)
- **Data Volume**: `redis_data` for persistent storage

### Redis Insight
- **Image**: `redislabs/redisinsight:latest`
- **Port**: 8001 (web interface)
- **Data Volume**: `redis_insight_data` for UI settings storage

## Usage

### Start Services
```bash
cd Docker
docker-compose up -d
```

### Setup Redis Insight Database Connection

After starting the services, you need to add the Redis database to Redis Insight:

1. **Access Redis Insight**: Open `http://localhost:8001` in your browser
2. **Complete Welcome**: If this is your first time, complete the welcome screen
3. **Add Database**:
   - Click "Add Database" button (+ icon)
   - Choose "Connect to a Redis Database"
4. **Enter Connection Details**:
   ```
   Host: redis
   Port: 6379
   Database Alias: Redis Workbench
   Username: (leave empty)
   Password: (leave empty)
   ```
5. **Test Connection**: Click "Test Connection" to verify
6. **Add Database**: Click "Add Redis Database"

**Troubleshooting Connection**:
- If `redis` hostname fails, try `host.docker.internal`
- If both fail, use the Redis container IP: `172.20.0.2`
- Ensure both containers are running: `docker ps`

### Stop Services
```bash
docker-compose down
```

### View Logs
```bash
# All services
docker-compose logs -f

# Redis only
docker-compose logs -f redis

# Redis Insight only
docker-compose logs -f redis-insight
```

### Connect to Redis

#### From your application
- **Host**: `localhost`
- **Port**: `6379`
- **Connection String**: `localhost:6379`

#### Redis Insight Web Interface
1. Open browser to `http://localhost:8001`
2. **First-time setup**: Complete the initial welcome screen
3. **Add Redis Database**:
   - Click "Add Database" or "Connect to a Redis Database"
   - Select "Connect to a Redis Database" (not Redis Cloud)
   - Fill in connection details:
     - **Host**: `redis` (container name - recommended)
     - **Port**: `6379`
     - **Database Alias**: `Redis Workbench` (or any name you prefer)
     - **Username**: Leave empty (no authentication configured)
     - **Password**: Leave empty (no authentication configured)
   - Click "Add Redis Database"

**Alternative connection options if `redis` doesn't work**:
- **Host**: `host.docker.internal` (Docker host from container)
- **Host**: `172.20.0.2` (Redis container IP)
- **Host**: `localhost` (only works from host machine, not from Redis Insight container)

#### Redis CLI
```bash
# Connect to running Redis container
docker exec -it redis-workbench redis-cli

# Or use Redis CLI from host (if installed)
redis-cli -h localhost -p 6379
```

## Data Persistence

- Redis data is persisted in the `redis_data` Docker volume
- Redis Insight settings are persisted in the `redis_insight_data` Docker volume
- Data survives container restarts and recreation

## Cleanup

### Remove containers and networks (keep data)
```bash
docker-compose down
```

### Remove everything including data volumes
```bash
docker-compose down -v
```

## Configuration

The Redis instance is configured with:
- AOF persistence enabled (`--appendonly yes`)
- Default Redis configuration
- No authentication (suitable for local development)

For production use, consider adding:
- Redis authentication
- SSL/TLS encryption
- Custom Redis configuration file
- Resource limits