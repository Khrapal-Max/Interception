//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Policies;

namespace Interception.Tests.Domain;

public sealed class DirectiveRelationPolicyTests
{
    [Fact]
    public void GetConfidenceScore_High_Returns300()
    {
        DirectiveRelationPolicy
            .GetConfidenceScore(DirectiveRelationConfidence.High)
            .Should().Be(300);
    }

    [Fact]
    public void GetTypeScore_Command_Returns60()
    {
        DirectiveRelationPolicy
            .GetTypeScore(DirectiveRelationType.Command)
            .Should().Be(60);
    }

    [Fact]
    public void BuildLabel_CommandHigh_ContainsReadableText()
    {
        var label = DirectiveRelationPolicy.BuildLabel(
            DirectiveRelationType.Command,
            DirectiveRelationConfidence.High);

        label.Should().Contain("явний наказ");
        label.Should().Contain("висока впевненість");
    }
}
