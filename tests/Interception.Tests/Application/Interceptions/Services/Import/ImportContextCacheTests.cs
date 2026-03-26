//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Services.Import;
using Interception.UI.Domain;

namespace Interception.Tests.Application.Interceptions.Services.Import;

public sealed class ImportContextCacheTests
{
    private static InterceptionAction MakeAction(string name)
        => InterceptionAction.Create(name, "");

    // -------------------------------------------------------------------------
    // FindAction
    // -------------------------------------------------------------------------

    [Fact]
    public void FindAction_ExactMatch_ReturnsAction()
    {
        var cache = new ImportContextCache([MakeAction("координація дій")]);

        var result = cache.FindAction("координація дій");

        result.Should().NotBeNull();
        result!.Name.Should().Be("координація дій");
    }

    [Fact]
    public void FindAction_CaseInsensitive_ReturnsAction()
    {
        var cache = new ImportContextCache([MakeAction("координація дій")]);

        cache.FindAction("КООРДИНАЦІЯ ДІЙ").Should().NotBeNull();
        cache.FindAction("Координація Дій").Should().NotBeNull();
    }

    [Fact]
    public void FindAction_TrimsWhitespace()
    {
        var cache = new ImportContextCache([MakeAction("координація дій")]);

        cache.FindAction("  координація дій  ").Should().NotBeNull();
    }

    [Fact]
    public void FindAction_NotFound_ReturnsNull()
    {
        var cache = new ImportContextCache([MakeAction("координація дій")]);

        cache.FindAction("невідома дія").Should().BeNull();
    }

    [Fact]
    public void FindAction_NullOrEmpty_ReturnsNull()
    {
        var cache = new ImportContextCache([MakeAction("координація дій")]);

        cache.FindAction(null).Should().BeNull();
        cache.FindAction("").Should().BeNull();
        cache.FindAction("   ").Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ResolveParticipantRole
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveParticipantRole_WithRole_ReturnsAndCaches()
    {
        var cache = new ImportContextCache([]);

        var role = cache.ResolveParticipantRole("ШАПКА", "центр (пехота)");

        role.Should().Be("центр (пехота)");
    }

    [Fact]
    public void ResolveParticipantRole_SecondCallWithoutRole_ReturnsCached()
    {
        var cache = new ImportContextCache([]);

        cache.ResolveParticipantRole("ШАПКА", "центр (пехота)");
        var role = cache.ResolveParticipantRole("ШАПКА", null);

        role.Should().Be("центр (пехота)");
    }

    [Fact]
    public void ResolveParticipantRole_NewRoleOverridesCached()
    {
        var cache = new ImportContextCache([]);

        cache.ResolveParticipantRole("ШАПКА", "стара роль");
        var role = cache.ResolveParticipantRole("ШАПКА", "нова роль");

        role.Should().Be("нова роль");
    }

    [Fact]
    public void ResolveParticipantRole_UnknownParticipant_ReturnsNull()
    {
        var cache = new ImportContextCache([]);

        var role = cache.ResolveParticipantRole(null, "роль");

        role.Should().BeNull();
    }

    [Fact]
    public void ResolveParticipantRole_NeverSeenWithoutRole_ReturnsNull()
    {
        var cache = new ImportContextCache([]);

        var role = cache.ResolveParticipantRole("НЕВІДОМИЙ", null);

        role.Should().BeNull();
    }

    [Fact]
    public void ResolveParticipantRole_CaseInsensitiveKey()
    {
        var cache = new ImportContextCache([]);

        cache.ResolveParticipantRole("шапка", "центр");
        var role = cache.ResolveParticipantRole("ШАПКА", null);

        role.Should().Be("центр");
    }
}
