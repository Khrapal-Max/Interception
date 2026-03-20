//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record SubdivisionReportFilterDto(
    DateTime? ObservedFrom = null,
    DateTime? ObservedTo = null,
    string? Query = null,
    bool IncludeRawUnresolved = true,
    bool IncludeWithoutSubdivision = false,
    int TopActions = 10,
    int TopActors = 10,
    int TopTags = 10,
    int SampleObservations = 5);
