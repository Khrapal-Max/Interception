//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;

namespace Interception.Tests.Components.Registry.InterceptionActions;

public sealed class InterceptionActionsLayeringTests
{
    [Fact]
    public void InterceptionActionsUi_DoesNotReferenceDomainNamespace()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var featurePath = Path.Combine(
            repoRoot,
            "src",
            "Interception.UI",
            "Components",
            "Pages",
            "Registry",
            "InterceptionActions");

        var files = Directory
            .EnumerateFiles(featurePath, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));

        var offenders = files
            .Where(path => File.ReadAllText(path).Contains("Interception.UI.Domain", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        offenders.Should().BeEmpty();
    }
}
