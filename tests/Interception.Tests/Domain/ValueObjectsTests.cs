//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain.ValueObjects;

namespace Interception.Tests.Domain;

public sealed class ValueObjectsTests
{
    [Fact]
    public void PersonName_Create_NormalizesAndBuildsLookupKey()
    {
        var value = PersonName.Create("  шапка-1  ");

        value.Should().NotBeNull();
        value!.Value.Value.Should().Be("шапка-1");
        value.Value.ToLookupKey().Should().Be("ШАПКА-1");
    }

    [Fact]
    public void FrequencyCode_Create_Empty_ReturnsNull()
    {
        FrequencyCode.Create("   ").Should().BeNull();
    }

    [Fact]
    public void DivisionName_Create_NormalizesValue()
    {
        var division = DivisionName.Create("  1 мсб  ");

        division.Should().NotBeNull();
        division!.Value.Value.Should().Be("1 мсб");
    }
}
