//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Interception.Application.Abstractions;

public interface IAppDbContext : IAsyncDisposable
{
    DbSet<InterceptionAction> InterceptionActions { get; }
    DbSet<InterceptionMessage> InterceptionMessages { get; }
    DbSet<InterceptionMessageLabel> InterceptionMessageLabels { get; }
    DbSet<InterceptionMessageParticipant> InterceptionMessageParticipants { get; }
    DbSet<ParticipantCandidateGroup> ParticipantCandidateGroups { get; }
    DbSet<ResolvedParticipant> ResolvedParticipants { get; }
    DbSet<DailyReport> DailyReports { get; }
    DbSet<MessageGroup> MessageGroups { get; }
    DbSet<ParticipantMatrix> ParticipantMatrices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
