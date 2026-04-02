//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Exports.Models;

namespace Interception.UI.Application.Exports.Dtos;

/// <summary>
/// Вхідні параметри Excel-експорту.
/// </summary>
public sealed record ExportRequestDto(
    ExportKind Kind,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    DateTime? Day = null);
