//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Import;

public sealed class ObservationImportResult
{
    public int TotalRows { get; internal set; }
    public int Inserted { get; internal set; }
    public int DuplicatesSkipped { get; internal set; }
    public int InvalidSkipped { get; internal set; }

    public List<ObservationImportError> Errors { get; } = new();
}
