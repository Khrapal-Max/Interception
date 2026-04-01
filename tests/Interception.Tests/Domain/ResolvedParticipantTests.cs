//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Domain.Entities;

namespace Interception.Tests.Domain;

public sealed class ResolvedParticipantTests
{
    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_SetsAllProperties()
    {
        var rp = ResolvedParticipant.Create(
            name: "ШАПКА",
            confirmedBy: "operator1",
            role: "центр (пехота)",
            division: "1 мсб 656 мсп");

        rp.Id.Should().NotBeEmpty();
        rp.Name.Should().Be("ШАПКА");
        rp.Role.Should().Be("центр (пехота)");
        rp.Division.Should().Be("1 мсб 656 мсп");
        rp.ConfirmedBy.Should().Be("operator1");
        rp.ConfirmedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithoutOptionalFields_NullRoleAndDivision()
    {
        var rp = ResolvedParticipant.Create("ВОЛГА", "operator1");

        rp.Role.Should().BeNull();
        rp.Division.Should().BeNull();
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var rp = ResolvedParticipant.Create(
            "  ШАПКА  ", "  operator1  ",
            role: "  центр  ", division: "  1 мсб  ");

        rp.Name.Should().Be("ШАПКА");
        rp.ConfirmedBy.Should().Be("operator1");
        rp.Role.Should().Be("центр");
        rp.Division.Should().Be("1 мсб");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ThrowsArgumentException(string name)
    {
        var act = () => ResolvedParticipant.Create(name, "operator1");

        act.Should().Throw<ArgumentException>()
           .WithParameterName(nameof(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyConfirmedBy_ThrowsArgumentException(string confirmedBy)
    {
        var act = () => ResolvedParticipant.Create("ШАПКА", confirmedBy);

        act.Should().Throw<ArgumentException>()
           .WithParameterName(nameof(confirmedBy));
    }

    [Fact]
    public void Create_WhitespaceRole_NormalizedToNull()
    {
        var rp = ResolvedParticipant.Create("ШАПКА", "op", role: "   ");

        rp.Role.Should().BeNull();
    }

    [Fact]
    public void Create_WhitespaceDivision_NormalizedToNull()
    {
        var rp = ResolvedParticipant.Create("ШАПКА", "op", division: "   ");

        rp.Division.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    [Fact]
    public void Update_ChangesNameRoleAndDivision()
    {
        var rp = ResolvedParticipant.Create("ШАПКА", "op", "стара роль", "старий підрозділ");

        rp.Update("ВОЛГА", "нова роль", "новий підрозділ");

        rp.Name.Should().Be("ВОЛГА");
        rp.Role.Should().Be("нова роль");
        rp.Division.Should().Be("новий підрозділ");
    }

    [Fact]
    public void Update_NullOptionalFields_ClearsThemToNull()
    {
        var rp = ResolvedParticipant.Create("ШАПКА", "op", "роль", "підрозділ");

        rp.Update("ШАПКА");

        rp.Role.Should().BeNull();
        rp.Division.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_EmptyName_ThrowsArgumentException(string name)
    {
        var rp = ResolvedParticipant.Create("ШАПКА", "op");

        var act = () => rp.Update(name);

        act.Should().Throw<ArgumentException>()
           .WithParameterName(nameof(name));
    }

    [Fact]
    public void Update_TrimsWhitespace()
    {
        var rp = ResolvedParticipant.Create("ШАПКА", "op");

        rp.Update("  ВОЛГА  ", "  роль  ", "  підрозділ  ");

        rp.Name.Should().Be("ВОЛГА");
        rp.Role.Should().Be("роль");
        rp.Division.Should().Be("підрозділ");
    }

    // -------------------------------------------------------------------------
    // Builder
    // -------------------------------------------------------------------------

    [Fact]
    public void Builder_CreatesResolvedParticipant()
    {
        var rp = new ResolvedParticipantBuilder()
            .WithName("КАРАСУК")
            .WithRole("взводний")
            .WithDivision("4 мсб 186 мсп")
            .Build();

        rp.Name.Should().Be("КАРАСУК");
        rp.Role.Should().Be("взводний");
        rp.Division.Should().Be("4 мсб 186 мсп");
    }
}
