//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Interception.UI.Domain;

/// <summary>
/// Append-only operator record (raw) that can be used for later grouping and analysis.
/// A record may contain only action, or action+location, or action+participants, etc.
/// </summary>
public class Observation
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateOnly ObservedDate { get; private set; }
    public DayPart DayPart { get; private set; }

    // Raw location/company snapshots (may be unknown).
    public decimal? Layer { get; private set; }          // e.g. "шар"
    public string? RmRaw { get; private set; }           // e.g. "р/м"
    public string? PointRaw { get; private set; }        // e.g. "точка" / coords

    public string? LocationRaw { get; private set; }     // e.g. "локація"
    public string? DistrictRaw { get; private set; }     // e.g. "район"
    public string? CompanyRaw { get; private set; }      // e.g. "фірма"

    // Action is the minimal required field for the record.
    public string ActionRaw { get; private set; } = default!;
    public string ActionNorm { get; private set; } = default!;

    /// <summary>
    /// Optional operator note / justification.
    /// </summary>
    public string? Note { get; private set; }

    // Import/registry metadata (optional).
    public string Source { get; private set; } = "manual";  // "manual" | "import"
    public Guid? SourceFileId { get; private set; }
    public int? SourceRow { get; private set; }

    /// <summary>
    /// Stable content hash for idempotent imports and deduplication.
    /// Computed from canonicalized core fields + participant labels.
    /// </summary>
    public string ContentHash { get; private set; } = default!;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<ObservationParticipant> Participants { get; private set; } = [];

    private Observation() { } // EF

    public static Observation Create(
        DateOnly observedDate,
        DayPart dayPart,
        string actionRaw,
        decimal? layer = null,
        string? rmRaw = null,
        string? pointRaw = null,
        string? locationRaw = null,
        string? districtRaw = null,
        string? companyRaw = null,
        string? note = null,
        string source = "manual",
        Guid? sourceFileId = null,
        int? sourceRow = null,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(actionRaw))
            throw new ArgumentException("Action is required.", nameof(actionRaw));

        var obs = new Observation
        {
            ObservedDate = observedDate,
            DayPart = dayPart,
            Layer = layer,
            RmRaw = string.IsNullOrWhiteSpace(rmRaw) ? null : rmRaw.Trim(),
            PointRaw = string.IsNullOrWhiteSpace(pointRaw) ? null : pointRaw.Trim(),
            LocationRaw = string.IsNullOrWhiteSpace(locationRaw) ? null : locationRaw.Trim(),
            DistrictRaw = string.IsNullOrWhiteSpace(districtRaw) ? null : districtRaw.Trim(),
            CompanyRaw = string.IsNullOrWhiteSpace(companyRaw) ? null : companyRaw.Trim(),
            ActionRaw = actionRaw.Trim(),
            ActionNorm = TextNorm.NormalizeRequired(actionRaw),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim(),
            SourceFileId = sourceFileId,
            SourceRow = sourceRow,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim(),
        };

        obs.ContentHash = obs.ComputeContentHash();
        return obs;
    }

    /// <summary>
    /// Adds a participant snapshot to this observation.
    /// For unknown participants you may pass null/empty labelRaw with isUnknown=true.
    /// </summary>
    public ObservationParticipant AddParticipant(string? labelRaw, bool isUnknown, string? roleRaw = null, int? ordinal = null)
    {
        var nextOrdinal = ordinal ?? (Participants.Count == 0 ? 1 : Participants.Max(p => p.Ordinal) + 1);

        // prevent duplicates by normalized label when known
        var norm = TextNorm.Normalize(labelRaw);
        if (norm is not null && Participants.Any(p => p.LabelNorm == norm))
            throw new InvalidOperationException($"Participant '{labelRaw}' already exists in this observation.");

        var p = new ObservationParticipant(Id, labelRaw, isUnknown, roleRaw, nextOrdinal);
        Participants.Add(p);

        // update hash because participants are part of canonical identity
        ContentHash = ComputeContentHash();
        return p;
    }

    private string ComputeContentHash()
    {
        // Canonical string: date|dayPart|actionNorm|locationNorm|districtNorm|companyNorm|rmNorm|pointNorm|layer|participants(sorted by ordinal)
        var sb = new StringBuilder();
        sb.Append(ObservedDate.ToString("yyyy-MM-dd")).Append('|');
        sb.Append((short)DayPart).Append('|');
        sb.Append(ActionNorm).Append('|');
        sb.Append(TextNorm.Normalize(LocationRaw) ?? "").Append('|');
        sb.Append(TextNorm.Normalize(DistrictRaw) ?? "").Append('|');
        sb.Append(TextNorm.Normalize(CompanyRaw) ?? "").Append('|');
        sb.Append(TextNorm.Normalize(RmRaw) ?? "").Append('|');
        sb.Append(TextNorm.Normalize(PointRaw) ?? "").Append('|');
        sb.Append(Layer?.ToString("0.####", CultureInfo.InvariantCulture) ?? "").Append('|');

        foreach (var p in Participants.OrderBy(x => x.Ordinal))
        {
            sb.Append(p.IsUnknown ? "u:" : "k:");
            sb.Append(p.LabelNorm ?? "").Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // uppercase hex
    }
}
