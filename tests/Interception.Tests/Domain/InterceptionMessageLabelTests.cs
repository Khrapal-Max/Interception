using FluentAssertions;
using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class InterceptionMessageLabelTests
{
    [Fact]
    public void Create_WithValidValue_ShouldTrimAndSetProperties()
    {
        var label = InterceptionMessageLabel.Create("  urgent  ");

        label.Id.Should().NotBeEmpty();
        label.NameLabel.Should().Be("urgent");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ShouldThrowArgumentException(string name)
    {
        var act = () => InterceptionMessageLabel.Create(name);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }
}
