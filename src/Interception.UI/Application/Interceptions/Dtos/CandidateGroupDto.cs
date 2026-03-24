//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>Група кандидатів для відображення на сторінці /analytics/candidates.</summary>
public sealed class CandidateGroupDto
{
    public Guid Id { get; init; }
    public CandidateGroupStatus Status { get; init; }
    public double ConfidenceScore { get; init; }

    /// <summary>Запропонована системою назва (позивний).</summary>
    public string? SuggestedName { get; init; }
    public string? SuggestedRole { get; init; }
    public string? SuggestedDivision { get; init; }

    /// <summary>Причини чому система згрупувала цих учасників.</summary>
    public CandidateGroupReasonsDto Reasons { get; init; } = new();

    /// <summary>Спостереження в яких зустрічається цей НВ.</summary>
    public List<CandidateGroupRefDto> Refs { get; init; } = [];

    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Id підтвердженої особи (після Confirm).</summary>
    public Guid? ResolvedParticipantId { get; init; }
}

/// <summary>Причини збігу — для відображення оператору.</summary>
public sealed class CandidateGroupReasonsDto
{
    public bool SameFrequency { get; init; }
    public bool SameVector { get; init; }
    public bool SamePointSignal { get; init; }
    public bool SameDivision { get; init; }
    public bool CloseInTime { get; init; }
    public bool SharedPartners { get; init; }
    public bool SharedLabels { get; init; }

    /// <summary>Людиночитабельний список активних причин.</summary>
    public IReadOnlyList<string> ActiveReasons
    {
        get
        {
            var list = new List<string>();
            if (SameFrequency) list.Add("однакова частота");
            if (SameVector) list.Add("однаковий вектор");
            if (SharedPartners) list.Add("спільні партнери");
            if (SamePointSignal) list.Add("однакова точка");
            if (SameDivision) list.Add("однаковий підрозділ");
            if (CloseInTime) list.Add("близько в часі");
            if (SharedLabels) list.Add("спільні мітки");
            return list;
        }
    }
}

/// <summary>Одне спостереження з групи — для деталей.</summary>
public sealed class CandidateGroupRefDto
{
    public Guid MessageId { get; init; }
    public Guid ParticipantId { get; init; }
    public int Ordinal { get; init; }
    public DateTime ObservedDate { get; init; }
    public string? Frequency { get; init; }
    public string? VectorSignal { get; init; }
    public string? Division { get; init; }
}
