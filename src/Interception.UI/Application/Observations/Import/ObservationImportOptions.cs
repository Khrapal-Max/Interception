//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Import;

public sealed class ObservationImportOptions
{
    public string Source { get; init; } = "import";
    public Guid? SourceFileId { get; init; }
    public string? CreatedBy { get; init; }

    /// <summary>
    /// If true, duplicates will be skipped using Observation.ContentHash.
    /// Requires a unique index on content_hash for full protection.
    /// </summary>
    public bool DeduplicateByHash { get; init; } = true;

    /// <summary>
    /// Expected to start at 1 for human-readable row numbers.
    /// If you import from a file with header, pass 2 to match Excel row numbers.
    /// </summary>
    public int FirstSourceRowNumber { get; init; } = 2;
}
