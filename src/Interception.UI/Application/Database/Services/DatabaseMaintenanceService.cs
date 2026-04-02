//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Database.Abstractions;
using Interception.UI.Application.Database.Dtos;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Database.Services;

/// <summary>
/// Сервіс файлових операцій над portable SQLite-базою.
/// </summary>
public sealed class DatabaseMaintenanceService(
    IDbContextFactory<AppDbContext> dbFactory,
    IWebHostEnvironment environment,
    ILogger<DatabaseMaintenanceService> logger) : IDatabaseMaintenanceService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<DatabaseMaintenanceService> _logger = logger;

    private string DataDirectoryPath => Path.Combine(_environment.ContentRootPath, "data");
    private string KeysDirectoryPath => Path.Combine(DataDirectoryPath, "keys");
    private string ExportDirectoryPath => Path.Combine(DataDirectoryPath, "exports");
    private string BackupDirectoryPath => Path.Combine(DataDirectoryPath, "backups");

    private string DatabasePath => Path.Combine(DataDirectoryPath, "interception.db");

    /// <inheritdoc />
    public async Task<DatabaseStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        EnsureDirectories();

        var dbFile = new FileInfo(DatabasePath);
        var walPath = DatabasePath + "-wal";
        var shmPath = DatabasePath + "-shm";

        var actionsCount = 0;
        var messagesCount = 0;
        var resolvedParticipantsCount = 0;

        if (dbFile.Exists)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                actionsCount = await db.InterceptionActions.CountAsync(ct);
                messagesCount = await db.InterceptionMessages.CountAsync(ct);
                resolvedParticipantsCount = await db.ResolvedParticipants.CountAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read SQLite status counters.");
            }
        }

        return new DatabaseStatusDto(
            DatabasePath,
            dbFile.Exists,
            dbFile.Exists ? dbFile.Length : 0,
            dbFile.Exists ? dbFile.LastWriteTimeUtc : null,
            Directory.Exists(KeysDirectoryPath),
            KeysDirectoryPath,
            actionsCount,
            messagesCount,
            resolvedParticipantsCount,
            File.Exists(walPath),
            File.Exists(shmPath));
    }

    /// <inheritdoc />
    public async Task<DatabaseExportDto> PrepareExportAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            EnsureDirectories();
            await CheckpointAsync(ct);

            if (!File.Exists(DatabasePath))
                throw new InvalidOperationException("Файл бази даних не знайдено.");

            var exportPath = Path.Combine(ExportDirectoryPath, "interception-export.db");
            if (File.Exists(exportPath))
                File.Delete(exportPath);

            File.Copy(DatabasePath, exportPath, overwrite: true);

            return new DatabaseExportDto(
                exportPath,
                $"interception-export-{DateTime.Now:yyyyMMdd-HHmmss}.db",
                DateTime.UtcNow);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DatabaseOperationResultDto> ImportAsync(
        Stream stream,
        string? fileName,
        string operatorName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        EnsureDirectories();

        var importName = string.IsNullOrWhiteSpace(fileName) ? "import.db" : Path.GetFileName(fileName);
        var tempImportPath = Path.Combine(ExportDirectoryPath, $"import-{Guid.NewGuid():N}-{importName}");

        await using (var file = File.Create(tempImportPath))
        {
            await stream.CopyToAsync(file, ct);
        }

        await Gate.WaitAsync(ct);
        try
        {
            await ValidateImportFileAsync(tempImportPath, ct);
            await CheckpointAsync(ct);

            var backupPath = await BackupCurrentDatabaseAsync("before-import", ct);

            SqliteConnection.ClearAllPools();
            DeleteSqliteSideFiles(DatabasePath);
            File.Copy(tempImportPath, DatabasePath, overwrite: true);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            await DatabaseSeedDefaults.EnsureActionSeedAsync(db, _logger, ct);

            return new DatabaseOperationResultDto(
                true,
                "Імпорт завершено",
                $"Базу даних замінено файлом '{importName}'.",
                backupPath);
        }
        finally
        {
            TryDelete(tempImportPath);
            Gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DatabaseOperationResultDto> ClearAsync(string operatorName, CancellationToken ct = default)
    {
        EnsureDirectories();

        await Gate.WaitAsync(ct);
        try
        {
            await CheckpointAsync(ct);
            var backupPath = await BackupCurrentDatabaseAsync("before-clear", ct);

            await using (var db = await _dbFactory.CreateDbContextAsync(ct))
            {
                await db.Database.EnsureDeletedAsync(ct);
            }

            SqliteConnection.ClearAllPools();
            DeleteSqliteSideFiles(DatabasePath);

            await using (var db = await _dbFactory.CreateDbContextAsync(ct))
            {
                await db.Database.EnsureCreatedAsync(ct);
                await DatabaseSeedDefaults.EnsureActionSeedAsync(db, _logger, ct);
            }

            return new DatabaseOperationResultDto(
                true,
                "Базу очищено",
                $"Порожню SQLite-базу створено повторно. Оператор: {operatorName}.",
                backupPath);
        }
        finally
        {
            Gate.Release();
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectoryPath);
        Directory.CreateDirectory(KeysDirectoryPath);
        Directory.CreateDirectory(ExportDirectoryPath);
        Directory.CreateDirectory(BackupDirectoryPath);
    }

    private async Task CheckpointAsync(CancellationToken ct)
    {
        if (!File.Exists(DatabasePath))
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (db.Database.IsSqlite())
                await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<string?> BackupCurrentDatabaseAsync(string suffix, CancellationToken ct)
    {
        if (!File.Exists(DatabasePath))
            return null;

        await CheckpointAsync(ct);
        var backupPath = Path.Combine(BackupDirectoryPath, $"interception-{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        File.Copy(DatabasePath, backupPath, overwrite: true);
        return backupPath;
    }

    private static async Task ValidateImportFileAsync(string filePath, CancellationToken ct)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = filePath }.ToString();
        await using var connection = new SqliteConnection(cs);
        await connection.OpenAsync(ct);

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    tables.Add(reader.GetString(0));
            }
        }

        if (!tables.Contains("interception_actions") || !tables.Contains("interception_messages"))
        {
            throw new InvalidOperationException("Обраний файл не схожий на SQLite-базу Interception.UI.");
        }
    }

    private static void DeleteSqliteSideFiles(string dbPath)
    {
        TryDelete(dbPath);
        TryDelete(dbPath + "-wal");
        TryDelete(dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
