//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Domain;

public sealed class DailyReportTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static InterceptionMessage MakeMessage(
        string? frequency = "149.500",
        string? vector = "NE",
        string? division = "1st Battalion",
        params string[] participantNames)
    {
        var msg = InterceptionMessage.Create(
            DateTime.UtcNow, frequency, division, vector,
            new InterceptionActionBuilder().Build(),
            note: null, createdBy: "op");

        foreach (var name in participantNames)
            msg.AddParticipant(name, isUnknown: false);

        return msg;
    }

    // -------------------------------------------------------------------------
    // Generate
    // -------------------------------------------------------------------------

    [Fact]
    public void Generate_WithMessages_ShouldCreateDraftReport()
    {
        var messages = new[]
        {
            MakeMessage("149.500", "NE", "1st Battalion", "Alpha", "Bravo"),
            MakeMessage("149.500", "NE", "1st Battalion", "Alpha", "Charlie")
        };

        var report = DailyReport.Generate(Today, messages, "operator1");

        report.Id.Should().NotBeEmpty();
        report.ReportDate.Should().Be(Today);
        report.TotalMessages.Should().Be(2);
        report.Status.Should().Be(DailyReportStatus.Draft);
        report.GeneratedBy.Should().Be("operator1");
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        report.PublishedAt.Should().BeNull();
        report.Matrix.Should().NotBeNull();
    }

    [Fact]
    public void Generate_WithEmptyList_ShouldCreateEmptyReport()
    {
        var report = DailyReport.Generate(Today, [], "op");

        report.TotalMessages.Should().Be(0);
        report.Groups.Should().BeEmpty();
        report.Matrix!.Cells.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ShouldGroupByFrequencyAndVector()
    {
        var messages = new[]
        {
            MakeMessage("149.500", "NE"),
            MakeMessage("149.500", "NE"),
            MakeMessage("156.000", "SW"),
        };

        var report = DailyReport.Generate(Today, messages, "op");

        report.Groups.Should().HaveCount(2);
        report.Groups.Should().ContainSingle(g => g.CommonFrequency == "149.500" && g.CommonVector == "NE");
        report.Groups.Should().ContainSingle(g => g.CommonFrequency == "156.000" && g.CommonVector == "SW");
    }

    [Fact]
    public void Generate_NullMessages_ShouldThrowArgumentNullException()
    {
        var act = () => DailyReport.Generate(Today, null!, "op");

        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // Publish
    // -------------------------------------------------------------------------

    [Fact]
    public void Publish_DraftReport_ShouldSetPublishedStatus()
    {
        var report = DailyReport.Generate(Today, [], "op");

        report.Publish("supervisor1");

        report.Status.Should().Be(DailyReportStatus.Published);
        report.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Publish_AlreadyPublished_ShouldThrowInvalidOperationException()
    {
        var report = DailyReport.Generate(Today, [], "op");
        report.Publish("supervisor1");

        var act = () => report.Publish("supervisor2");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Publish_WithEmptyOperator_ShouldThrowArgumentException(string op)
    {
        var report = DailyReport.Generate(Today, [], "op");

        var act = () => report.Publish(op);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("publishedBy");
    }
}

public sealed class ParticipantMatrixTests
{
    // -------------------------------------------------------------------------
    // Build
    // -------------------------------------------------------------------------

    private static InterceptionMessage MakeMessage(params string[] names)
    {
        var msg = InterceptionMessage.Create(
            DateTime.UtcNow, "149.500", "div", "NE",
            new InterceptionActionBuilder().Build(),
            null, "op");

        foreach (var name in names)
            msg.AddParticipant(name, isUnknown: false);

        return msg;
    }

    [Fact]
    public void Build_TwoParticipantsInOneMessage_ShouldCreateOneCell()
    {
        var messages = new[] { MakeMessage("Alpha", "Bravo") };
        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);

        matrix.Cells.Should().HaveCount(1);

        var cell = matrix.Cells.Single();
        cell.InteractionCount.Should().Be(1);
        new[] { cell.ParticipantA, cell.ParticipantB }
            .Should().BeEquivalentTo(["Alpha", "Bravo"]);
    }

    [Fact]
    public void Build_SamePairInTwoMessages_ShouldIncrementCount()
    {
        var messages = new[]
        {
            MakeMessage("Alpha", "Bravo"),
            MakeMessage("Alpha", "Bravo")
        };

        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);

        matrix.Cells.Should().HaveCount(1);
        matrix.Cells.Single().InteractionCount.Should().Be(2);
    }

    [Fact]
    public void Build_ThreeParticipantsInOneMessage_ShouldCreateThreeCells()
    {
        // 3 учасники → 3 пари: (A,B), (A,C), (B,C)
        var messages = new[] { MakeMessage("Alpha", "Bravo", "Charlie") };
        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);

        matrix.Cells.Should().HaveCount(3);
    }

    [Fact]
    public void Build_CellsShouldBeSymmetric_ParticipantAAlwaysLessThanB()
    {
        // "Zulu" > "Alpha" лексикографічно — матриця має впорядкувати їх
        var messages = new[] { MakeMessage("Zulu", "Alpha") };
        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);

        var cell = matrix.Cells.Single();
        string.Compare(cell.ParticipantA, cell.ParticipantB, StringComparison.Ordinal)
            .Should().BeNegative("ParticipantA завжди має бути лексикографічно менший");
    }

    [Fact]
    public void Build_WithNoParticipants_ShouldReturnEmptyMatrix()
    {
        var msg = InterceptionMessage.Create(
            DateTime.UtcNow, "149.500", "div", "NE",
            new InterceptionActionBuilder().Build(), null, "op");

        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), [msg]);

        matrix.Cells.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetInteractionsFor
    // -------------------------------------------------------------------------

    [Fact]
    public void GetInteractionsFor_KnownParticipant_ShouldReturnPartners()
    {
        var messages = new[]
        {
            MakeMessage("Alpha", "Bravo"),
            MakeMessage("Alpha", "Charlie"),
            MakeMessage("Alpha", "Bravo")
        };

        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);
        var results = matrix.GetInteractionsFor("Alpha").ToList();

        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.Partner == "Bravo" && r.Count == 2);
        results.Should().ContainSingle(r => r.Partner == "Charlie" && r.Count == 1);
        // Відсортовано за спаданням
        results[0].Count.Should().BeGreaterThanOrEqualTo(results[1].Count);
    }

    [Fact]
    public void GetInteractionsFor_UnknownParticipant_ShouldReturnEmpty()
    {
        var messages = new[] { MakeMessage("Alpha", "Bravo") };
        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);

        var results = matrix.GetInteractionsFor("Delta").ToList();

        results.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetTopPairs
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTopPairs_ShouldReturnMostActivePairsFirst()
    {
        var messages = new[]
        {
            MakeMessage("Alpha", "Bravo"),
            MakeMessage("Alpha", "Bravo"),
            MakeMessage("Alpha", "Bravo"),
            MakeMessage("Charlie", "Delta"),
        };

        var matrix = ParticipantMatrix.Build(Guid.NewGuid(), messages);
        var top = matrix.GetTopPairs(1).ToList();

        top.Should().HaveCount(1);
        top[0].InteractionCount.Should().Be(3);
        new[] { top[0].ParticipantA, top[0].ParticipantB }
            .Should().BeEquivalentTo(["Alpha", "Bravo"]);
    }
}
