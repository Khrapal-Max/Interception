//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Коренева DTO-модель окремого зрізу по групах карти зв'язків.
/// </summary>
public sealed record LinkMapSliceDto(
    IReadOnlyList<LinkMapGroupSliceDto> Groups);

/// <summary>
/// Зріз конкретної комунікаційної групи:
/// склад осіб + спільні частоти + група дій + прогалини даних.
/// </summary>
public sealed record LinkMapGroupSliceDto(
    string GroupKey,
    string KeyPersonName,
    string? Division,
    bool IsDivisionMissing,
    int MembersCount,
    int MissingRoleCount,
    int CompleteProfileCount,
    IReadOnlyList<string> Members,
    IReadOnlyList<string> SharedFrequencies,
    IReadOnlyList<string> SharedActions);
