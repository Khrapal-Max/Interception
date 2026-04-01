//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class InterceptionMessageTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static InterceptionAction MakeAction() => new InterceptionActionBuilder().Build();

    private static InterceptionMessage MakeMessage(
        InterceptionAction? action = null,
        DateTime? observedDate = null) =>
        InterceptionMessage.Create(
            observedDate ?? DateTime.UtcNow,
            frequency: "149.500",
            division: "1st Battalion",
            vectorSignal: "NE",
            interceptionAction: action ?? MakeAction(),
            note: "test note",
            createdBy: "operator1",
            pointSignal: "Grid 1234");

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ShouldReturnMessage()
    {
        var action = MakeAction();
        var message = MakeMessage(action);

        message.Id.Should().NotBeEmpty();
        message.Frequency.Should().Be("149.500");
        message.Division.Should().Be("1st Battalion");
        message.VectorSignal.Should().Be("NE");
        message.PointSignal.Should().Be("Grid 1234");
        message.Note.Should().Be("test note");
        message.CreatedBy.Should().Be("operator1");
        message.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        message.UpdatedAt.Should().BeNull();
        message.InterceptionAction.Should().Be(action);
        message.InterceptionActionId.Should().Be(action.Id);
    }

    [Fact]
    public void Create_WithNullAction_ShouldThrowArgumentNullException()
    {
        var act = () => InterceptionMessage.Create(
            DateTime.UtcNow, "149.500", "div", "NE",
            interceptionAction: null!,
            note: null, createdBy: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("  149.500  ", "149.500")]
    [InlineData("  ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Create_ShouldNormalizeOptionalStrings(string? input, string? expected)
    {
        var message = InterceptionMessage.Create(
            DateTime.UtcNow, input, input, input,
            MakeAction(), input, "op");

        message.Frequency.Should().Be(expected);
        message.Division.Should().Be(expected);
        message.VectorSignal.Should().Be(expected);
        message.Note.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    [Fact]
    public void Update_WithValidData_ShouldChangeFields()
    {
        var message = MakeMessage();
        var newAction = MakeAction();
        var newDate = DateTime.UtcNow.AddHours(-2);

        message.Update(newDate, "156.000", "2nd Battalion", "SW", newAction, "updated note");

        message.ObservedDate.Should().Be(newDate);
        message.Frequency.Should().Be("156.000");
        message.Division.Should().Be("2nd Battalion");
        message.VectorSignal.Should().Be("SW");
        message.InterceptionAction.Should().Be(newAction);
        message.InterceptionActionId.Should().Be(newAction.Id);
        message.Note.Should().Be("updated note");
        message.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_ShouldNotChangeCreatedByOrCreatedAt()
    {
        var message = MakeMessage();
        var createdBy = message.CreatedBy;
        var createdAt = message.CreatedAt;

        message.Update(DateTime.UtcNow, "156.000", null, null, MakeAction(), null);

        message.CreatedBy.Should().Be(createdBy);
        message.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Update_WithNullAction_ShouldThrowArgumentNullException()
    {
        var message = MakeMessage();
        var act = () => message.Update(DateTime.UtcNow, null, null, null, null!, null);

        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // AddParticipant
    // -------------------------------------------------------------------------

    [Fact]
    public void AddParticipant_FirstParticipant_ShouldHaveOrdinal1()
    {
        var message = MakeMessage();
        var participant = message.AddParticipant("Alpha", isUnknown: false);

        participant.Ordinal.Should().Be(1);
        message.Participants.Should().HaveCount(1);
    }

    [Fact]
    public void AddParticipant_SecondParticipant_ShouldAutoIncrementOrdinal()
    {
        var message = MakeMessage();
        message.AddParticipant("Alpha", isUnknown: false);
        var second = message.AddParticipant("Bravo", isUnknown: false);

        second.Ordinal.Should().Be(2);
        message.Participants.Should().HaveCount(2);
    }

    [Fact]
    public void AddParticipant_WithExplicitOrdinal_ShouldUseProvidedOrdinal()
    {
        var message = MakeMessage();
        var participant = message.AddParticipant("Alpha", isUnknown: false, ordinal: 5);

        participant.Ordinal.Should().Be(5);
    }

    [Fact]
    public void AddParticipant_DuplicateKnownName_ShouldThrowInvalidOperationException()
    {
        var message = MakeMessage();
        message.AddParticipant("Alpha", isUnknown: false);

        var act = () => message.AddParticipant("Alpha", isUnknown: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Alpha*");
    }

    [Fact]
    public void AddParticipant_DuplicateKnownName_CaseInsensitive_ShouldThrow()
    {
        var message = MakeMessage();
        message.AddParticipant("alpha", isUnknown: false);

        var act = () => message.AddParticipant("ALPHA", isUnknown: false);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddParticipant_MultipleUnknown_ShouldAllowDuplicates()
    {
        var message = MakeMessage();
        message.AddParticipant(null, isUnknown: true);
        message.AddParticipant(null, isUnknown: true);

        message.Participants.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // RemoveParticipant
    // -------------------------------------------------------------------------

    [Fact]
    public void RemoveParticipant_ExistingId_ShouldRemoveFromList()
    {
        var message = MakeMessage();
        var participant = message.AddParticipant("Alpha", isUnknown: false);

        message.RemoveParticipant(participant.Id);

        message.Participants.Should().BeEmpty();
    }

    [Fact]
    public void RemoveParticipant_UnknownId_ShouldThrowInvalidOperationException()
    {
        var message = MakeMessage();
        var act = () => message.RemoveParticipant(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    // -------------------------------------------------------------------------
    // AddLabel / RemoveLabel
    // -------------------------------------------------------------------------

    [Fact]
    public void AddLabel_ValidName_ShouldAddToList()
    {
        var message = MakeMessage();
        var label = message.AddLabel("urgent");

        label.NameLabel.Should().NotBeNullOrWhiteSpace();
        message.Labels.Should().HaveCount(1);
    }

    [Fact]
    public void AddLabel_DuplicateName_ShouldThrowInvalidOperationException()
    {
        var message = MakeMessage();
        message.AddLabel("urgent");

        var act = () => message.AddLabel("urgent");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*urgent*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLabel_EmptyOrWhitespace_ShouldThrowArgumentException(string name)
    {
        var message = MakeMessage();
        var act = () => message.AddLabel(name);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveLabel_ExistingId_ShouldRemoveFromList()
    {
        var message = MakeMessage();
        var label = message.AddLabel("urgent");

        message.RemoveLabel(label.Id);

        message.Labels.Should().BeEmpty();
    }

    [Fact]
    public void RemoveLabel_UnknownId_ShouldThrowInvalidOperationException()
    {
        var message = MakeMessage();
        var act = () => message.RemoveLabel(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
