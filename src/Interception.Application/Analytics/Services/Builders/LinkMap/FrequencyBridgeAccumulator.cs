//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Services.Builders.LinkMap;

internal sealed class FrequencyBridgeAccumulator(string frequency)
{
    public string Frequency { get; } = frequency;
    public int Weight { get; set; }
    public NameCounter LeftContacts { get; } = new();
    public NameCounter RightContacts { get; } = new();
}
