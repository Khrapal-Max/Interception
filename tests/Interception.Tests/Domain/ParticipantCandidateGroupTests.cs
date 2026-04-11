//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain.Analytics;
using Interception.UI.Domain.Analytics.Enums;
using Interception.UI.Domain.Analytics.Records;
using Interception.UI.Domain.Exceptions;

namespace Interception.Tests.Domain;

public sealed class ParticipantCandidateGroupTests
{
    private static List<ParticipantRef> MakeRefs(int count = 2)
        => [.. Enumerable.Range(1, count)
            .Select(i => new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), i))];

    private static PatternMatchReasons MakeReasons(
        bool freq = true,
        bool vector = true,
        bool point = false,
        bool div = false,
        bool time = true,
        bool partners = false,
        bool labels = false)
        => new()
        {
            SameFrequency = freq,
            SameVector = vector,
            SamePointSignal = point,
            SameDivision = div,
            CloseInTime = time,
            SharedPartners = partners,
            SharedLabels = labels
        };

    [Fact]
    public void Create_WithValidData_ShouldReturnOpenGroup()
    {
        var refs = MakeRefs();
        var reasons = MakeReasons();

        var group = ParticipantCandidateGroup.Create(
            refs,
            0.75,
            reasons,
            suggestedName: "Alpha",
            suggestedRole: "центр",
            suggestedDivision: "1 мсб");

        group.Id.Should().NotBeEmpty();
        group.Status.Should().Be(CandidateGroupStatus.Open);
        group.ConfidenceScore.Should().Be(0.75);
        group.SuggestedName.Should().Be("Alpha");
        group.SuggestedRole.Should().Be("центр");
        group.SuggestedDivision.Should().Be("1 мсб");
        group.ParticipantRefs.Should().HaveCount(2);
        group.ResolvedParticipantId.Should().BeNull();
        group.ResolvedBy.Should().BeNull();
        group.ResolvedAt.Should().BeNull();
        group.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithSingleRef_ShouldThrowArgumentException()
    {
        var act = () => ParticipantCandidateGroup.Create(MakeRefs(1), 0.50, MakeReasons());

        act.Should().Throw<ArgumentException>()
            .WithParameterName("refs");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_WithInvalidConfidence_ShouldThrowArgumentOutOfRangeException(double score)
    {
        var act = () => ParticipantCandidateGroup.Create(MakeRefs(), score, MakeReasons());

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("confidenceScore");
    }

    [Fact]
    public void Create_WhitespaceSuggestedFields_ShouldNormalizeToNull()
    {
        var group = ParticipantCandidateGroup.Create(
            MakeRefs(),
            0.5,
            MakeReasons(),
            suggestedName: "   ",
            suggestedRole: "   ",
            suggestedDivision: "   ");

        group.SuggestedName.Should().BeNull();
        group.SuggestedRole.Should().BeNull();
        group.SuggestedDivision.Should().BeNull();
    }

    [Fact]
    public void Confirm_OpenGroup_ShouldSetConfirmedStatusAndResolutionData()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        var resolvedParticipantId = Guid.NewGuid();

        group.Confirm("  ГРОМ  ", "  operator1  ", resolvedParticipantId);

        group.Status.Should().Be(CandidateGroupStatus.Confirmed);
        group.SuggestedName.Should().Be("ГРОМ");
        group.ResolvedBy.Should().Be("operator1");
        group.ResolvedParticipantId.Should().Be(resolvedParticipantId);
        group.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirm_WithEmptyResolvedName_ShouldThrowArgumentException(string resolvedName)
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());

        var act = () => group.Confirm(resolvedName, "operator1", Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(resolvedName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirm_WithEmptyResolvedBy_ShouldThrowArgumentException(string resolvedBy)
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());

        var act = () => group.Confirm("ГРОМ", resolvedBy, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(resolvedBy));
    }

    [Fact]
    public void Confirm_AfterDismiss_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        group.Dismiss("operator1");

        var act = () => group.Confirm("ГРОМ", "operator2", Guid.NewGuid());

        act.Should().Throw<AggregateStateViolationException>()
            .WithMessage("*Dismissed*");
    }

    [Fact]
    public void Dismiss_OpenGroup_ShouldSetDismissedStatus()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());

        group.Dismiss("  operator1  ");

        group.Status.Should().Be(CandidateGroupStatus.Dismissed);
        group.ResolvedBy.Should().Be("operator1");
        group.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        group.ResolvedParticipantId.Should().BeNull();
    }

    [Fact]
    public void Dismiss_AfterConfirm_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        group.Confirm("ГРОМ", "operator1", Guid.NewGuid());

        var act = () => group.Dismiss("operator2");

        act.Should().Throw<AggregateStateViolationException>()
            .WithMessage("*Confirmed*");
    }

    [Fact]
    public void UpdateSuggestedFields_OpenGroup_ShouldUpdateIndividually()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());

        group.UpdateSuggestedName("  БОНИК  ");
        group.UpdateSuggestedRole("  оператор  ");
        group.UpdateSuggestedDivision("  1 мсб  ");

        group.SuggestedName.Should().Be("БОНИК");
        group.SuggestedRole.Should().Be("оператор");
        group.SuggestedDivision.Should().Be("1 мсб");
    }

    [Fact]
    public void UpdateSuggestedFields_WithWhitespace_ShouldClearValues()
    {
        var group = ParticipantCandidateGroup.Create(
            MakeRefs(),
            0.80,
            MakeReasons(),
            suggestedName: "Alpha",
            suggestedRole: "role",
            suggestedDivision: "division");

        group.UpdateSuggestedName("  ");
        group.UpdateSuggestedRole(null);
        group.UpdateSuggestedDivision("");

        group.SuggestedName.Should().BeNull();
        group.SuggestedRole.Should().BeNull();
        group.SuggestedDivision.Should().BeNull();
    }

    [Fact]
    public void UpdateSuggestedFields_OnConfirmedGroup_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        group.Confirm("ГРОМ", "operator1", Guid.NewGuid());

        var act = () => group.UpdateSuggestedRole("оператор");

        act.Should().Throw<AggregateStateViolationException>()
            .WithMessage("*Confirmed*");
    }

    [Fact]
    public void AddRef_NewParticipant_ShouldAppendReference()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        var newRef = new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 3);

        group.AddRef(newRef);

        group.ParticipantRefs.Should().Contain(newRef);
        group.ParticipantRefs.Should().HaveCount(3);
    }

    [Fact]
    public void AddRef_DuplicateParticipantId_ShouldIgnoreDuplicate()
    {
        var refs = MakeRefs();
        var group = ParticipantCandidateGroup.Create(refs, 0.80, MakeReasons());
        var duplicate = new ParticipantRef(Guid.NewGuid(), refs[0].ParticipantId, 99);

        group.AddRef(duplicate);

        group.ParticipantRefs.Should().HaveCount(2);
    }

    [Fact]
    public void AddRef_OnDismissedGroup_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        group.Dismiss("operator1");

        var act = () => group.AddRef(new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 3));

        act.Should().Throw<AggregateStateViolationException>()
            .WithMessage("*Dismissed*");
    }

    [Fact]
    public void UpdateScore_OpenGroup_ShouldReplaceScoreAndReasons()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.40, MakeReasons(freq: true, vector: false));
        var newReasons = MakeReasons(freq: true, vector: true, div: true, labels: true);

        group.UpdateScore(0.90, newReasons);

        group.ConfidenceScore.Should().Be(0.90);
        group.Reasons.Should().BeEquivalentTo(newReasons);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void UpdateScore_WithInvalidConfidence_ShouldThrowArgumentOutOfRangeException(double score)
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.40, MakeReasons());

        var act = () => group.UpdateScore(score, MakeReasons());

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("confidenceScore");
    }

    [Fact]
    public void UpdateScore_OnConfirmedGroup_ShouldThrowInvalidOperationException()
    {
        var group = ParticipantCandidateGroup.Create(MakeRefs(), 0.80, MakeReasons());
        group.Confirm("ГРОМ", "operator1", Guid.NewGuid());

        var act = () => group.UpdateScore(0.50, MakeReasons(freq: false));

        act.Should().Throw<AggregateStateViolationException>()
            .WithMessage("*Confirmed*");
    }
}
