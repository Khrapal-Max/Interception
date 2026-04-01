//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Domain.Enums;

/// <summary>Статус щоденного звіту.</summary>
public enum DailyReportStatus
{
    /// <summary>Щойно згенерований, ще не опублікований.</summary>
    Draft = 0,

    /// <summary>Опублікований — доступний операторам.</summary>
    Published = 1,

    /// <summary>Замінений новим звітом за той самий день.</summary>
    Superseded = 2
}