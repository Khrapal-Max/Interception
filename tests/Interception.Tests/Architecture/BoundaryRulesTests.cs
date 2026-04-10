//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;

namespace Interception.Tests.Architecture;

public sealed class BoundaryRulesTests
{
    [Fact]
    public void Components_ShouldNotReference_DomainOrInfrastructureNamespaces()
    {
        var componentsPath = ResolvePath("src", "Interception.UI", "Components");
        var offenders = FindNamespaceReferences(
            componentsPath,
            [
                "Interception.UI.Domain",
                "Interception.UI.Infrastructure"
            ]);

        offenders.Should().BeEmpty("UI layer must stay isolated from Domain/Infrastructure details");
    }

    [Fact]
    public void Application_ShouldNotReference_ComponentsNamespace()
    {
        var applicationPath = ResolvePath("src", "Interception.UI", "Application");
        var offenders = FindNamespaceReferences(applicationPath, ["Interception.UI.Components"]);

        offenders.Should().BeEmpty("Application layer must not depend on UI components");
    }

    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrComponentsNamespaces()
    {
        var domainPath = ResolvePath("src", "Interception.UI", "Domain");
        var offenders = FindNamespaceReferences(
            domainPath,
            [
                "Interception.UI.Application",
                "Interception.UI.Components"
            ]);

        offenders.Should().BeEmpty("Domain must remain independent from upper layers");
    }

    private static string ResolvePath(params string[] segments)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        return Path.Combine([repoRoot, .. segments]);
    }

    private static string[] FindNamespaceReferences(string rootPath, IReadOnlyCollection<string> forbiddenNamespaces)
    {
        var files = Directory
            .EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));

        return files
            .Where(path =>
            {
                var content = File.ReadAllText(path);
                return forbiddenNamespaces.Any(ns => content.Contains(ns, StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(rootPath, path))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
