//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Services;

namespace Interception.Tests.Application.Observations;

public sealed class ObservationLookupServiceTests
{
    [Fact]
    public async Task AllMethods_ReturnEmpty_ForBlankQuery()
    {
        var factory = TestDbFactory.CreateFactory();
        var sut = new ObservationLookupService(factory);

        Assert.Empty(await sut.GetLayerSuggestionsAsync("   ", 10, CancellationToken.None));
        Assert.Empty(await sut.GetDistrictSuggestionsAsync(string.Empty, 10, CancellationToken.None));
        Assert.Empty(await sut.SearchParticipantSuggestionsAsync(" ", 10, CancellationToken.None));
        Assert.Empty(await sut.SearchActionTextSuggestionsAsync(null!, 10, CancellationToken.None));
        Assert.Empty(await sut.SearchActionCatalogSuggestionsAsync("   ", 10, CancellationToken.None));
        Assert.Empty(await sut.SearchSubdivisionSuggestionsAsync("", 10, CancellationToken.None));
    }
}
