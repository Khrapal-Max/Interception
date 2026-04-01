using FluentAssertions;
using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class InterceptionActionTests
{
    [Fact]
    public void Create_WithValidData_ShouldTrimAndSetProperties()
    {
        var action = InterceptionAction.Create("  Recon  ", "  Description  ");

        action.Id.Should().NotBeEmpty();
        action.Name.Should().Be("Recon");
        action.Description.Should().Be("Description");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ShouldThrowArgumentException(string name)
    {
        var act = () => InterceptionAction.Create(name, "desc");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithNullDescription_ShouldUseEmptyString()
    {
        var action = InterceptionAction.Create("Recon", null!);

        action.Description.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithValidData_ShouldTrimAndUpdateFields()
    {
        var action = InterceptionAction.Create("Old", "Old desc");

        action.Update("  New  ", "  New desc  ");

        action.Name.Should().Be("New");
        action.Description.Should().Be("New desc");
    }

    [Fact]
    public void Update_WithNullDescription_ShouldUseEmptyString()
    {
        var action = InterceptionAction.Create("Name", "desc");

        action.Update("Name", null!);

        action.Description.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithEmptyName_ShouldThrowArgumentException(string name)
    {
        var action = InterceptionAction.Create("Name", "desc");
        var act = () => action.Update(name, "next");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }
}
