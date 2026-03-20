//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class UnknownClusterTests
{
    [Fact]
    public void Create_Rename_Note_Members_And_Resolution_Work()
    {
        var cluster = UnknownCluster.Create("  НВ водій  ", note: "  первинна гіпотеза  ", createdBy: "  analyst  ");

        cluster.Rename("  НВ командир  ");
        cluster.SetNote("  уточнено  ");

        var participantId = Guid.NewGuid();
        var member = cluster.AddMember(participantId, note: "  з observation  ");

        cluster.ResolveToActor(Guid.NewGuid());
        Assert.NotNull(cluster.ResolvedActorId);
        Assert.NotNull(cluster.ArchivedAtUtc);

        cluster.Reopen();
        cluster.Archive();

        Assert.Equal("НВ командир", cluster.Title);
        Assert.Equal("нв командир", cluster.TitleNorm);
        Assert.Equal("уточнено", cluster.Note);
        Assert.Single(cluster.Members);
        Assert.Equal(participantId, member.ObservationParticipantId);
        Assert.NotNull(cluster.ArchivedAtUtc);
    }

    [Fact]
    public void AddMember_Rejects_Duplicate_Participant()
    {
        var cluster = UnknownCluster.Create("НВ водій");
        var participantId = Guid.NewGuid();
        cluster.AddMember(participantId);

        var ex = Assert.Throws<InvalidOperationException>(() => cluster.AddMember(participantId));

        Assert.Contains("already belongs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Member_Move_And_Note_Update_Work()
    {
        var cluster = UnknownCluster.Create("НВ");
        var member = cluster.AddMember(Guid.NewGuid(), note: "первинно");
        var newClusterId = Guid.NewGuid();

        member.MoveToCluster(newClusterId);
        member.SetNote("  перенесено  ");

        Assert.Equal(newClusterId, member.UnknownClusterId);
        Assert.Equal("перенесено", member.Note);
    }
}
