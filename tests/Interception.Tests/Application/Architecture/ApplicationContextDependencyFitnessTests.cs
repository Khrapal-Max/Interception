//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;

namespace Interception.Tests.Application.Architecture;

public sealed class ApplicationContextDependencyFitnessTests
{
    [Fact]
    public void ApplicationServices_DoNotDependOnOtherContextServices()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var appPath = Path.Combine(repoRoot, "src", "Interception.UI", "Application");

        var contextRoots = Directory
            .EnumerateDirectories(appPath)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var files = Directory
            .EnumerateFiles(appPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        var offenders = new List<string>();

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(appPath, file);
            var currentContext = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(currentContext))
                continue;

            var text = File.ReadAllText(file);

            foreach (var otherContext in contextRoots.Where(x => !string.Equals(x, currentContext, StringComparison.Ordinal)))
            {
                var forbiddenUsing = $"using Interception.UI.Application.{otherContext}.Services;";
                if (text.Contains(forbiddenUsing, StringComparison.Ordinal))
                    offenders.Add($"{relative} -> {forbiddenUsing}");
            }
        }

        offenders.Should().BeEmpty("cross-context залежності мають йти через Abstractions/DTO/events, а не через Services namespace");
    }

    [Fact]
    public void ApplicationAbstractions_DoNotExposeDomainEntitiesInContracts()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var abstractionsPath = Path.Combine(repoRoot, "src", "Interception.UI", "Application");

        var files = Directory
            .EnumerateFiles(abstractionsPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Abstractions{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        var offenders = files
            .Where(path => File.ReadAllText(path).Contains("using Interception.UI.Domain;", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(abstractionsPath, path))
            .ToArray();

        offenders.Should().BeEmpty("public Application API має повертати DTO/read-model або примітиви");
    }
}
