//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;

namespace Interception.Tests.Components;

public sealed class UiLayeringTests
{
    [Fact]
    public void ComponentsUi_DoesNotReferenceDomainNamespace()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var componentsPath = Path.Combine(repoRoot, "src", "Interception.UI", "Components");

        var files = Directory
            .EnumerateFiles(componentsPath, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));

        var offenders = files
            .Where(path => File.ReadAllText(path).Contains("Interception.UI.Domain", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(componentsPath, path))
            .ToArray();

        offenders.Should().BeEmpty();
    }
}
