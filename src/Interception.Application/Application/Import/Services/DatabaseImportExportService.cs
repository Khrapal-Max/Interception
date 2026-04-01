//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.Json;
using Interception.UI.Application.Import.Abstractions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Interception.UI.Application.Import.Services;

/// <summary>
/// Реалізація повного backup/restore БД через PostgreSQL COPY та JSON-контейнер.
/// </summary>
public sealed class DatabaseImportExportService(
    IDbContextFactory<AppDbContext> dbFactory) : IDatabaseImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public async Task ExportAsync(Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var conn = await OpenNpgsqlConnectionAsync(db, cancellationToken);

        var tableNames = await GetTableNamesAsync(conn, cancellationToken);
        var tables = new List<TableDump>(tableNames.Count);

        foreach (var tableName in tableNames)
        {
            var columns = await GetColumnNamesAsync(conn, tableName, cancellationToken);
            var csvData = await ExportTableToCsvAsync(conn, tableName, columns, cancellationToken);
            tables.Add(new TableDump(tableName, columns, csvData));
        }

        var dump = new DatabaseDump(DateTime.UtcNow, tables);
        await JsonSerializer.SerializeAsync(output, dump, JsonOptions, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ImportAsync(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var dump = await JsonSerializer.DeserializeAsync<DatabaseDump>(input, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Неможливо прочитати backup JSON.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var conn = await OpenNpgsqlConnectionAsync(db, cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            await SetReplicationRoleAsync(conn, "replica", cancellationToken);
            await TruncateAllTablesAsync(conn, dump.Tables.Select(x => x.TableName).ToArray(), cancellationToken);

            foreach (var table in dump.Tables)
                await ImportTableFromCsvAsync(conn, table, cancellationToken);

            await SetReplicationRoleAsync(conn, "origin", cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await SetReplicationRoleAsync(conn, "origin", cancellationToken);
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<NpgsqlConnection> OpenNpgsqlConnectionAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection npgsqlConnection)
            throw new NotSupportedException("Сервіс backup/restore підтримує лише PostgreSQL.");

        if (npgsqlConnection.State != System.Data.ConnectionState.Open)
            await npgsqlConnection.OpenAsync(cancellationToken);

        return npgsqlConnection;
    }

    private static async Task<List<string>> GetTableNamesAsync(NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));

        return result;
    }

    private static async Task<string[]> GetColumnNamesAsync(NpgsqlConnection conn, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            ORDER BY ordinal_position;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));

        return result.ToArray();
    }

    private static async Task<string> ExportTableToCsvAsync(
        NpgsqlConnection conn,
        string tableName,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        if (columns.Count == 0)
            return string.Empty;

        var columnsSql = string.Join(", ", columns.Select(QuoteIdentifier));
        var copySql = $"COPY public.{QuoteIdentifier(tableName)} ({columnsSql}) TO STDOUT WITH (FORMAT CSV, NULL '\\N')";

        using var reader = conn.BeginTextExport(copySql);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task ImportTableFromCsvAsync(
        NpgsqlConnection conn,
        TableDump table,
        CancellationToken cancellationToken)
    {
        if (table.Columns.Length == 0 || string.IsNullOrWhiteSpace(table.CsvData))
            return;

        var columnsSql = string.Join(", ", table.Columns.Select(QuoteIdentifier));
        var copySql = $"COPY public.{QuoteIdentifier(table.TableName)} ({columnsSql}) FROM STDIN WITH (FORMAT CSV, NULL '\\N')";

        using var writer = conn.BeginTextImport(copySql);
        await writer.WriteAsync(table.CsvData.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task TruncateAllTablesAsync(NpgsqlConnection conn, IReadOnlyCollection<string> tables, CancellationToken cancellationToken)
    {
        if (tables.Count == 0)
            return;

        var tableListSql = string.Join(", ", tables.Select(x => $"public.{QuoteIdentifier(x)}"));
        var sql = $"TRUNCATE TABLE {tableListSql} RESTART IDENTITY CASCADE;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetReplicationRoleAsync(NpgsqlConnection conn, string role, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand($"SET session_replication_role = {role};", conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private sealed record DatabaseDump(DateTime ExportedAtUtc, List<TableDump> Tables);

    private sealed record TableDump(string TableName, string[] Columns, string CsvData);
}
