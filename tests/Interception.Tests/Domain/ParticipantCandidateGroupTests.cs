//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;

namespace Interception.Tests.Domain;

public sealed class ParticipantCandidateGroupTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static List<ParticipantRef> MakeRefs(int count = 2) =>
        [.. Enumerable.Range(1, count)
            .Select(_ => new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1))];

    private static PatternMatchReasons MakeReasons(
        bool freq = true, bool vector = true, bool point = false,
        bool div = false, bool time = true,
        bool partners = false, bool labels = false) =>
        new()
        {
            SameFrequency = freq,
            SameVector = vector,
            SamePointSignal = point,
            SameDivision = div,
            CloseInTime = time,
            SharedPartners = partners,
            SharedLabels = labels
        };

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ShouldReturnOpenGroup()
    {
        var refs = MakeRefs();
        var reasons = MakeReasons();

        var group = ParticipantCandidateGroup.Create(
            refs, 0.75, reasons,
            suggestedName: "Alpha", suggestedRole: "центр", suggestedDivision: "1 мсб");

        group.Id.Should().NotBeEmpty();
        group.Status.Should().Be(CandidateGroupStatus.Open);
        group.ConfidenceScore.Should().Be(0.75);
        group.SuggestedName.Should().Be("Alpha");
        group.SuggestedRole.Should().Be("центр");
        group.SuggestedDivision.Should().Be("1 мсб");
        group.ParticipantRefs.Should().HaveCount(2);
        group.ResolvedBy.Should().BeNull();
        group.ResolvedAt.Should().BeNull();
        group.ResolvedParticipantId.Should().BeNull();
        group.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithSingleRef_ShouldThrowArgumentException()
    {
        var act = () => ParticipantCandidateGroup.Create(
            MakeRefs(1), 0.75, MakeReasons());

        act.Should().Throw<ArgumentException>()
           .WithMessage("*два*");
    }

    [Fact]
    public void Create_WithNullRefs_ShouldThrowArgumentNullException()
    {
        var act = () => ParticipantCandidateGroup.Create(
            null!, 0.75, MakeReasons());

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(2.0)]
    public void Create_WithInvalidConfidence_ShouldThrowArgumentOutOfRangeException(double score)
    {
        var act = () => ParticipantCandidateGroup.Create(
            MakeRefs(), score, MakeReasons());

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("confidenceScore");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_WithBoundaryConfidence_ShouldSucceed(double score)
    {
        var act = () => ParticipantCandidateGroup.Create(MakeRefs(), score, MakeReasons());

        act.Should().NotThrow();
    }

    [Fact]
    public void Create_WhitespaceSuggestedFields_NormalizedToNull()
    {
        var group = ParticipantCandidateGroup.Create(
            MakeRefs(), 0.5, MakeReasons(),
            suggestedName: "   ", suggestedRole: "   ", suggestedDivision: "   ");

        group.SuggestedName.Should().BeNull();
        group.SuggestedRole.Should().BeNull();
        group.SuggestedDivision.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Confirm
    // -------------------------------------------------------------------------

    [Fact]
    public void Confirm_OpenGroup_SetsConfirmedStatusAndResolvedParticipantId()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());
        var resolvedId = Guid.NewGuid();

        group.Confirm("Alpha", "operator1", resolvedId);

        group.Status.Should().Be(CandidateGroupStatus.Confirmed);
        group.SuggestedName.Should().Be("Alpha");
        group.ResolvedBy.Should().Be("operator1");
        group.ResolvedParticipantId.Should().Be(resolvedId);
        group.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirm_WithEmptyName_ShouldThrowArgumentException(string name)
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());

        var act = () => group.Confirm(name, "operator1", Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
           .WithParameterName("resolvedName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirm_WithEmptyOperator_ShouldThrowArgumentException(string op)
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());

        var act = () => group.Confirm("Alpha", op, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
           .WithParameterName("resolvedBy");
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());
        group.Confirm("Alpha", "operator1", Guid.NewGuid());

        var act = () => group.Confirm("Bravo", "operator2", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Confirmed*");
    }

    // -------------------------------------------------------------------------
    // Dismiss
    // -------------------------------------------------------------------------

    [Fact]
    public void Dismiss_OpenGroup_ShouldSetDismissedStatus()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());

        group.Dismiss("operator1");

        group.Status.Should().Be(CandidateGroupStatus.Dismissed);
        group.ResolvedBy.Should().Be("operator1");
        group.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        group.ResolvedParticipantId.Should().BeNull();
    }

    [Fact]
    public void Dismiss_AlreadyDismissed_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());
        group.Dismiss("operator1");

        var act = () => group.Dismiss("operator2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dismiss_AfterConfirm_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());
        group.Confirm("Alpha", "operator1", Guid.NewGuid());

        var act = () => group.Dismiss("operator2");

        act.Should().Throw<InvalidOperationException>();
    }

    // -------------------------------------------------------------------------
    // UpdateSuggestions (замінює старий UpdateSuggestedName)
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateSuggestions_OpenGroup_UpdatesAllFields()
    {
        var group = ParticipantCandidateGroup.Create(
            MakeRefs(), 0.8, MakeReasons(), "Alpha", "стара роль", "старий підрозділ");

        group.UpdateSuggestions("Bravo", "нова роль", "новий підрозділ");

        group.SuggestedName.Should().Be("Bravo");
        group.SuggestedRole.Should().Be("нова роль");
        group.SuggestedDivision.Should().Be("новий підрозділ");
    }

    [Fact]
    public void UpdateSuggestions_NullValues_ClearsToNull()
    {
        var group = ParticipantCandidateGroup.Create(
            MakeRefs(), 0.8, MakeReasons(), "Alpha", "роль", "підрозділ");

        group.UpdateSuggestions(null, null, null);

        group.SuggestedName.Should().BeNull();
        group.SuggestedRole.Should().BeNull();
        group.SuggestedDivision.Should().BeNull();
    }

    [Fact]
    public void UpdateSuggestions_OnConfirmedGroup_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.8, MakeReasons());
        group.Confirm("Alpha", "operator1", Guid.NewGuid());

        var act = () => group.UpdateSuggestions("Bravo");

        act.Should().Throw<InvalidOperationException>();
    }
}
