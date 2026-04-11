//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Import.Services;
using Interception.UI.Domain.Entities;

namespace Interception.Tests.Application.Import;

public sealed class ImportContextCacheTests
{
    private static InterceptionAction MakeAction(string name)
        => InterceptionAction.Create(name, "");

    private static ImportContextCache CreateCache(
        IEnumerable<InterceptionAction>? actions = null,
        IReadOnlyDictionary<string, string>? roleMap = null)
        => new(actions ?? [], roleMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    // -------------------------------------------------------------------------
    // FindAction
    // -------------------------------------------------------------------------

    [Fact]
    public void FindAction_ExactMatch_ReturnsAction()
    {
        var cache = CreateCache([MakeAction("координація дій")]);

        var result = cache.FindAction("координація дій");

        result.Should().NotBeNull();
        result!.Name.Should().Be("координація дій");
    }

    [Fact]
    public void FindAction_CaseInsensitive_ReturnsAction()
    {
        var cache = CreateCache([MakeAction("координація дій")]);

        cache.FindAction("КООРДИНАЦІЯ ДІЙ").Should().NotBeNull();
        cache.FindAction("Координація Дій").Should().NotBeNull();
    }

    [Fact]
    public void FindAction_TrimsWhitespace()
    {
        var cache = CreateCache([MakeAction("координація дій")]);

        cache.FindAction("  координація дій  ").Should().NotBeNull();
    }

    [Fact]
    public void FindAction_NotFound_ReturnsNull()
    {
        var cache = CreateCache([MakeAction("координація дій")]);

        cache.FindAction("невідома дія").Should().BeNull();
    }

    [Fact]
    public void FindAction_NullOrEmpty_ReturnsNull()
    {
        var cache = CreateCache([MakeAction("координація дій")]);

        cache.FindAction(null).Should().BeNull();
        cache.FindAction("").Should().BeNull();
        cache.FindAction("   ").Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ResolveRole
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveRole_WhenRoleExistsInCatalog_ReturnsCatalogValue()
    {
        var cache = CreateCache(
            roleMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ОПЕРАТОР БПЛА"] = "оператор БПЛА"
            });

        var result = cache.ResolveRole("  оператор бпла  ");

        result.Should().Be("оператор БПЛА");
    }

    [Fact]
    public void ResolveRole_WhenRoleNotInCatalog_ReturnsNormalizedInput()
    {
        var cache = CreateCache();

        var result = cache.ResolveRole("  центр (піхота)  ");

        result.Should().Be("центр (піхота)");
    }

    [Fact]
    public void ResolveRole_CaseInsensitive_MatchesCatalog()
    {
        var cache = CreateCache(
            roleMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["КОМАНДИР"] = "Командир"
            });

        cache.ResolveRole("командир").Should().Be("Командир");
        cache.ResolveRole("КОМАНДИР").Should().Be("Командир");
        cache.ResolveRole("  Командир  ").Should().Be("Командир");
    }

    [Fact]
    public void ResolveRole_NullOrWhitespace_ReturnsNull()
    {
        var cache = CreateCache();

        cache.ResolveRole(null).Should().BeNull();
        cache.ResolveRole("").Should().BeNull();
        cache.ResolveRole("   ").Should().BeNull();
    }
}
