//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Interceptions;
using Interception.UI.Extensions;

namespace Interception.UI.Domain.Analytics;

/// <summary>
/// Член канонічної особи — прив'язка до підтвердженого registry row.
/// </summary>
public sealed class CanonicalPersonMember
{
    public Guid Id { get; private set; }
    public Guid CanonicalPersonId { get; private set; }
    public CanonicalPerson CanonicalPerson { get; private set; } = default!;

    public Guid ResolvedParticipantId { get; private set; }
    public ResolvedParticipant ResolvedParticipant { get; private set; } = default!;

    public DateTime AddedAtUtc { get; private set; }

    public static CanonicalPersonMember Create(Guid canonicalPersonId, Guid resolvedParticipantId)
        => new()
        {
            Id = Guid.NewGuid(),
            CanonicalPersonId = canonicalPersonId,
            ResolvedParticipantId = resolvedParticipantId,
            AddedAtUtc = ConverterDateTimeExtensions.Now
        };
}
