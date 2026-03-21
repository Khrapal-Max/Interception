//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class InterceptionMessageParticipantTests
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithValidKnownParticipant_ShouldSetProperties()
    {
        var msgId       = Guid.NewGuid();
        var participant = new InterceptionMessageParticipantBuilder()
            .WithMessageId(msgId)
            .WithName("Alpha")
            .WithRole("commander")
            .WithOrdinal(1)
            .Build();

        participant.Id.Should().NotBeEmpty();
        participant.InterceptionMessageId.Should().Be(msgId);
        participant.Name.Should().Be("Alpha");
        participant.Role.Should().Be("commander");
        participant.Ordinal.Should().Be(1);
        participant.IsUnknown.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithIsUnknownTrue_ShouldSetIsUnknownTrue()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName("Alpha")
            .WithIsUnknown(true)
            .Build();

        participant.IsUnknown.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullName_ShouldBeUnknown()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName(null)
            .WithIsUnknown(false)
            .Build();

        // null name → effectiveUnknown = true незалежно від isUnknown
        participant.IsUnknown.Should().BeTrue();
        participant.Name.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidOrdinal_ShouldThrowArgumentOutOfRangeException(int ordinal)
    {
        var act = () => new InterceptionMessageParticipantBuilder()
            .WithOrdinal(ordinal)
            .Build();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(ordinal));
    }

    [Theory]
    [InlineData("  Alpha  ", "Alpha")]
    [InlineData("  ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Constructor_ShouldNormalizeName(string? input, string? expected)
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName(input)
            .WithIsUnknown(false)
            .Build();

        participant.Name.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // UpdateSnapshot
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateSnapshot_WithNewName_ShouldUpdateName()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName("Alpha")
            .Build();

        participant.UpdateSnapshot("Bravo", "scout", isUnknown: false);

        participant.Name.Should().Be("Bravo");
        participant.Role.Should().Be("scout");
        participant.IsUnknown.Should().BeFalse();
    }

    [Fact]
    public void UpdateSnapshot_SetNameToNull_ShouldBecomeUnknown()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName("Alpha")
            .Build();

        participant.UpdateSnapshot(null, null, isUnknown: false);

        participant.IsUnknown.Should().BeTrue();
        participant.Name.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ResolveAsKnown
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveAsKnown_WithValidName_ShouldSetKnown()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName(null)
            .WithIsUnknown(true)
            .Build();

        participant.ResolveAsKnown("Alpha", "commander");

        participant.Name.Should().Be("Alpha");
        participant.Role.Should().Be("commander");
        participant.IsUnknown.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveAsKnown_WithEmptyName_ShouldThrowArgumentException(string name)
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithIsUnknown(true)
            .Build();

        var act = () => participant.ResolveAsKnown(name);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }

    // -------------------------------------------------------------------------
    // UpdateRole
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateRole_WithNewRole_ShouldUpdateRole()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName("Alpha")
            .WithRole("scout")
            .Build();

        participant.UpdateRole("sniper");

        participant.Role.Should().Be("sniper");
    }

    [Fact]
    public void UpdateRole_WithNull_ShouldClearRole()
    {
        var participant = new InterceptionMessageParticipantBuilder()
            .WithName("Alpha")
            .WithRole("scout")
            .Build();

        participant.UpdateRole(null);

        participant.Role.Should().BeNull();
    }
}
