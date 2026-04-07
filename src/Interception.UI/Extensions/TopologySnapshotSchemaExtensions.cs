//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Extensions;

/// <summary>
/// Додає відсутні таблиці snapshot-ів топології для вже існуючої portable SQLite БД.
/// </summary>
public static class TopologySnapshotSchemaExtensions
{
    public static async Task EnsureTopologySnapshotSchemaAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS topology_snapshot_runs (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_runs PRIMARY KEY,
                date_from_utc TEXT NULL,
                date_to_utc TEXT NULL,
                status TEXT NOT NULL,
                is_stale INTEGER NOT NULL DEFAULT 0,
                group_count INTEGER NOT NULL DEFAULT 0,
                error_message TEXT NULL,
                created_at TEXT NOT NULL,
                completed_at TEXT NULL
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_runs_status ON topology_snapshot_runs(status);",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_runs_completed_at ON topology_snapshot_runs(completed_at);",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_runs_period_status ON topology_snapshot_runs(date_from_utc, date_to_utc, status);",

            @"CREATE TABLE IF NOT EXISTS topology_snapshot_groups (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_groups PRIMARY KEY,
                run_id TEXT NOT NULL,
                group_key TEXT NOT NULL,
                division TEXT NULL,
                key_person_name TEXT NOT NULL,
                key_person_role TEXT NULL,
                mention_count INTEGER NOT NULL,
                internal_connection_weight INTEGER NOT NULL,
                bridge_weight INTEGER NOT NULL,
                sort_order INTEGER NOT NULL,
                CONSTRAINT fk_topology_snapshot_groups_run_id FOREIGN KEY (run_id) REFERENCES topology_snapshot_runs(id) ON DELETE CASCADE
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_groups_run_id ON topology_snapshot_groups(run_id);",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_groups_run_group_key ON topology_snapshot_groups(run_id, group_key);",

            @"CREATE TABLE IF NOT EXISTS topology_snapshot_group_frequencies (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_group_frequencies PRIMARY KEY,
                group_id TEXT NOT NULL,
                frequency TEXT NOT NULL,
                sort_order INTEGER NOT NULL,
                CONSTRAINT fk_topology_snapshot_group_frequencies_group_id FOREIGN KEY (group_id) REFERENCES topology_snapshot_groups(id) ON DELETE CASCADE
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_group_frequencies_group_id ON topology_snapshot_group_frequencies(group_id);",

            @"CREATE TABLE IF NOT EXISTS topology_snapshot_group_members (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_group_members PRIMARY KEY,
                group_id TEXT NOT NULL,
                name TEXT NOT NULL,
                role TEXT NULL,
                mention_count INTEGER NOT NULL,
                unique_partner_count INTEGER NOT NULL,
                connection_weight INTEGER NOT NULL,
                last_seen_at TEXT NOT NULL,
                group_count INTEGER NOT NULL,
                is_shared_across_groups INTEGER NOT NULL,
                is_key_person INTEGER NOT NULL,
                sort_order INTEGER NOT NULL,
                CONSTRAINT fk_topology_snapshot_group_members_group_id FOREIGN KEY (group_id) REFERENCES topology_snapshot_groups(id) ON DELETE CASCADE
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_group_members_group_id ON topology_snapshot_group_members(group_id);",

            @"CREATE TABLE IF NOT EXISTS topology_snapshot_group_actions (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_group_actions PRIMARY KEY,
                group_id TEXT NOT NULL,
                name TEXT NOT NULL,
                is_primary INTEGER NOT NULL,
                sort_order INTEGER NOT NULL,
                CONSTRAINT fk_topology_snapshot_group_actions_group_id FOREIGN KEY (group_id) REFERENCES topology_snapshot_groups(id) ON DELETE CASCADE
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_group_actions_group_id ON topology_snapshot_group_actions(group_id);",

            @"CREATE TABLE IF NOT EXISTS topology_snapshot_bridges (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_bridges PRIMARY KEY,
                group_id TEXT NOT NULL,
                target_group_key TEXT NOT NULL,
                target_division TEXT NULL,
                contact_person_name TEXT NOT NULL,
                bridge_frequency TEXT NOT NULL,
                weight INTEGER NOT NULL,
                primary_action TEXT NULL,
                sort_order INTEGER NOT NULL,
                CONSTRAINT fk_topology_snapshot_bridges_group_id FOREIGN KEY (group_id) REFERENCES topology_snapshot_groups(id) ON DELETE CASCADE
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_bridges_group_id ON topology_snapshot_bridges(group_id);",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_bridges_group_target_group ON topology_snapshot_bridges(group_id, target_group_key);",

            @"CREATE TABLE IF NOT EXISTS topology_snapshot_bridge_actions (
                id TEXT NOT NULL CONSTRAINT pk_topology_snapshot_bridge_actions PRIMARY KEY,
                bridge_id TEXT NOT NULL,
                name TEXT NOT NULL,
                is_primary INTEGER NOT NULL,
                sort_order INTEGER NOT NULL,
                CONSTRAINT fk_topology_snapshot_bridge_actions_bridge_id FOREIGN KEY (bridge_id) REFERENCES topology_snapshot_bridges(id) ON DELETE CASCADE
            );",
            @"CREATE INDEX IF NOT EXISTS ix_topology_snapshot_bridge_actions_bridge_id ON topology_snapshot_bridge_actions(bridge_id);"
        };

        foreach (var command in commands)
            await db.Database.ExecuteSqlRawAsync(command, ct);

        logger.LogInformation("Topology snapshot schema ensured successfully.");
    }
}
