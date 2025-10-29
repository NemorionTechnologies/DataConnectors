# Running Database Migrations

## Overview

The DataWorkflows project uses a custom migration system that automatically applies SQL migrations from embedded resources. Migrations are stored in `src/DataWorkflows.Data/Migrations/` and tracked in the `schema_migrations` table.

## Migration Runner Location

The `MigrationRunner.ApplyAll(connectionString)` method is located in:
```
src/DataWorkflows.Data/Migrations/MigrationRunner.cs
```

## How Migrations Work

1. **Embedded Resources**: All `*.sql` files in the `Migrations` folder are embedded into the DataWorkflows.Data.dll assembly at build time
2. **Tracking Table**: The `schema_migrations` table tracks which migrations have been applied
3. **Automatic Application**: Migrations are applied in alphabetical order by filename
4. **Idempotent**: Running migrations multiple times is safe - already-applied migrations are skipped

## Step-by-Step Guide: Running Migrations from Different Locations

### Option 1: From Engine API Startup (Current Default)

The migrations run automatically when the Engine API starts.

**Location**: `src/DataWorkflows.Engine.Api/Program.cs` (lines 70-80)

```csharp
// Apply database migrations at startup
try
{
    var connStr = builder.Configuration.GetConnectionString("Postgres")!;
    await MigrationRunner.ApplyAll(connStr);
}
catch (Exception ex)
{
    Console.WriteLine($"Migration apply failed: {ex.Message}");
    throw;
}
```

**When to use**: Standard workflow - migrations run before the API starts accepting requests.

---

### Option 2: Create a Standalone Migration Tool

**When to use**: You want to run migrations separately from the application (CI/CD, database initialization, etc.)

**Steps**:

1. **Create a new console project**:
   ```bash
   cd tools
   dotnet new console -n MigrateDatabaseTool
   cd MigrateDatabaseTool
   ```

2. **Add reference to DataWorkflows.Data**:
   ```bash
   dotnet add reference ../../src/DataWorkflows.Data/DataWorkflows.Data.csproj
   ```

3. **Update `Program.cs`**:
   ```csharp
   using DataWorkflows.Data.Migrations;

   if (args.Length < 1)
   {
       Console.WriteLine("Usage: MigrateDatabaseTool <connection-string>");
       Console.WriteLine("Example: MigrateDatabaseTool \"Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres\"");
       return 1;
   }

   var connectionString = args[0];

   try
   {
       Console.WriteLine("Applying database migrations...");
       await MigrationRunner.ApplyAll(connectionString);
       Console.WriteLine("✓ Migrations applied successfully!");
       return 0;
   }
   catch (Exception ex)
   {
       Console.WriteLine($"✗ Migration failed: {ex.Message}");
       if (ex.InnerException != null)
       {
           Console.WriteLine($"  Details: {ex.InnerException.Message}");
       }
       return 1;
   }
   ```

4. **Run the tool**:
   ```bash
   dotnet run --project tools/MigrateDatabaseTool -- "Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres"
   ```

---

### Option 3: From Any C# Application

**When to use**: You want to embed migrations in another service or startup process.

**Steps**:

1. **Add package reference**:
   ```bash
   dotnet add reference path/to/DataWorkflows.Data/DataWorkflows.Data.csproj
   ```

2. **Add using statement**:
   ```csharp
   using DataWorkflows.Data.Migrations;
   ```

3. **Call the migration runner**:
   ```csharp
   var connectionString = "Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres";

   try
   {
       await MigrationRunner.ApplyAll(connectionString);
       Console.WriteLine("Migrations applied successfully");
   }
   catch (Exception ex)
   {
       Console.WriteLine($"Migration failed: {ex.Message}");
       throw;
   }
   ```

---

### Option 4: Using PowerShell/Bash Script

**When to use**: Quick one-off migration runs or testing.

**PowerShell Script** (`tools/apply-migrations.ps1`):
```powershell
$connectionString = "Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres"

Write-Host "Applying database migrations..." -ForegroundColor Cyan

$result = dotnet run --project "$PSScriptRoot/../src/DataWorkflows.Engine.Api" -- migrate $connectionString

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Migrations applied successfully!" -ForegroundColor Green
} else {
    Write-Host "✗ Migration failed!" -ForegroundColor Red
    exit 1
}
```

**Bash Script** (`tools/apply-migrations.sh`):
```bash
#!/bin/bash
CONNECTION_STRING="Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres"

echo "Applying database migrations..."

dotnet run --project "$(dirname "$0")/../src/DataWorkflows.Engine.Api" -- migrate "$CONNECTION_STRING"

if [ $? -eq 0 ]; then
    echo "✓ Migrations applied successfully!"
else
    echo "✗ Migration failed!"
    exit 1
fi
```

---

## Connection String Formats

### Local Development (Docker)
```
Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres
```

### Environment Variables
```csharp
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? "Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres";
```

### Configuration File (appsettings.json)
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres"
  }
}
```

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();
var connectionString = configuration.GetConnectionString("Postgres");
```

---

## Troubleshooting

### Problem: "Migration apply failed: relation already exists"

**Cause**: The database tables exist but aren't tracked in `schema_migrations`.

**Solution**: Manually mark migrations as applied:
```sql
-- Connect to database
docker exec -it dataworkflows-postgres psql -U postgres -d dataworkflows

-- Check which migrations are tracked
SELECT name FROM schema_migrations ORDER BY name;

-- Check which tables exist
\dt

-- If tables exist but migrations aren't tracked, manually insert records
INSERT INTO schema_migrations(name) VALUES
    ('DataWorkflows.Data.Migrations.001_CreateWorkflows.sql'),
    ('DataWorkflows.Data.Migrations.002_CreateWorkflowDefinitions.sql');
-- etc.
```

### Problem: "Failed to connect to 127.0.0.1:5432"

**Cause**: PostgreSQL isn't running or wrong port.

**Solution**:
1. Start PostgreSQL:
   ```bash
   docker start dataworkflows-postgres
   ```
2. Verify port mapping:
   ```bash
   docker ps | grep postgres
   ```
3. Update connection string to use correct port (likely 5433)

### Problem: "Migration X failed to apply"

**Cause**: SQL error in migration file or database state issue.

**Solution**:
1. Check the migration file for syntax errors
2. Review the error message for details
3. Manually inspect database state:
   ```bash
   docker exec -it dataworkflows-postgres psql -U postgres -d dataworkflows
   ```
4. If needed, rollback changes and fix the migration

---

## Adding New Migrations

1. **Create a new `.sql` file** in `src/DataWorkflows.Data/Migrations/`:
   ```
   009_YourMigrationName.sql
   ```

   Use sequential numbering: `001`, `002`, `003`, etc.

2. **Write your SQL**:
   ```sql
   -- Migration 009: Add your changes
   CREATE TABLE YourNewTable (
       Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
       Name TEXT NOT NULL,
       CreatedAt TIMESTAMPTZ DEFAULT NOW()
   );

   COMMENT ON TABLE YourNewTable IS 'Description of your table';
   ```

3. **Rebuild the Data project** (migrations are embedded resources):
   ```bash
   dotnet build src/DataWorkflows.Data
   ```

4. **Run migrations**:
   - Restart the Engine API (automatic), OR
   - Use one of the methods above

5. **Verify**:
   ```sql
   SELECT name FROM schema_migrations WHERE name LIKE '%009%';
   ```

---

## Migration File Naming Convention

- **Format**: `XXX_DescriptiveName.sql`
- **XXX**: Zero-padded sequential number (001, 002, 003, ...)
- **DescriptiveName**: PascalCase description of what the migration does
- **Examples**:
  - `001_CreateWorkflows.sql`
  - `002_CreateWorkflowDefinitions.sql`
  - `008_CreateActionCatalog.sql`

**Important**: Migrations are applied in **alphabetical order**, so numbering is critical!

---

## Best Practices

1. ✅ **Always rebuild DataWorkflows.Data** after adding migrations
2. ✅ **Test migrations on a local database** before production
3. ✅ **Make migrations idempotent** when possible (use `IF NOT EXISTS`, etc.)
4. ✅ **Never modify existing migration files** - create new ones instead
5. ✅ **Include comments** in migration files explaining the changes
6. ✅ **Keep migrations small and focused** - one logical change per migration
7. ✅ **Version control** all migration files
8. ✅ **Document breaking changes** in migration comments

---

## Current Database Schema

To view the current database schema:

```bash
# List all tables
docker exec dataworkflows-postgres psql -U postgres -d dataworkflows -c "\dt"

# View table structure
docker exec dataworkflows-postgres psql -U postgres -d dataworkflows -c "\d tablename"

# View all applied migrations
docker exec dataworkflows-postgres psql -U postgres -d dataworkflows -c "SELECT name, applied_at FROM schema_migrations ORDER BY applied_at"
```

---

## Environment-Specific Connection Strings

### Development (Local Docker)
```
Host=localhost;Port=5433;Database=dataworkflows;Username=postgres;Password=postgres
```

### Staging/Production
Store in environment variables:
```bash
export DATABASE_CONNECTION_STRING="Host=production-host;Port=5432;Database=dataworkflows;Username=app_user;Password=secure_password"
```

Access in code:
```csharp
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("DATABASE_CONNECTION_STRING not configured");

await MigrationRunner.ApplyAll(connectionString);
```
