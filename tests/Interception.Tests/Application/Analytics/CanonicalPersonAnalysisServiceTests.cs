//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Analytics;

/// <summary>
/// Тести для сервісу канонічних осіб.
/// Перевіряють лише сценарії, де канонічна особа потрібна для проблемних випадків,
/// а не для всіх записів реєстру.
/// </summary>
public sealed class CanonicalPersonAnalysisServiceTests
{
    private static CanonicalPersonAnalysisService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task GetCandidatesAsync_ReturnsOnlyProblematicCandidates_AndMarksExistingCanonical()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        Guid linkedRowId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var sh1 = ResolvedParticipant.Create("ШАПКА", "operator", role: "командир", division: "336 мсп");
            var sh2 = ResolvedParticipant.Create("шапка", "operator", role: "командир", division: "336 мсп");
            var bars = ResolvedParticipant.Create("БАРС", "operator", role: "оператор", division: "19 омбр");

            linkedRowId = sh1.Id;

            db.ResolvedParticipants.AddRange(sh1, sh2, bars);

            var canonical = CanonicalPerson.Create("ШАПКА", "вже зведено");
            canonical.AddMember(sh1.Id);
            db.CanonicalPersons.Add(canonical);

            db.InterceptionMessages.AddRange(
                CreateMessage(action, new DateTime(2026, 04, 08, 10, 0, 0, DateTimeKind.Utc), "402.0000", "336 мсп", "ШАПКА"),
                CreateMessage(action, new DateTime(2026, 04, 08, 10, 5, 0, DateTimeKind.Utc), "145.1000", "336 мсп", "Шапка"),
                CreateMessage(action, new DateTime(2026, 04, 08, 11, 0, 0, DateTimeKind.Utc), "401.2000", "19 омбр", "БАРС"));

            await db.SaveChangesAsync(ct);
        }

        var result = await service.GetCandidatesAsync(ct);

        result.Should().ContainSingle("лише ШАПКА є проблемним випадком для зведення");
        var candidate = result.Single();

        candidate.DisplayName.Should().Be("ШАПКА");
        candidate.ConfirmedRowsCount.Should().Be(2);
        candidate.DistinctFrequencyCount.Should().Be(2);
        candidate.DistinctDivisionCount.Should().Be(1);
        candidate.HasCanonicalPerson.Should().BeTrue();
        candidate.CanonicalPersonId.Should().NotBeNull();
        candidate.LastConfirmedAtUtc.Should().NotBeNull();

        var details = await service.GetCandidateDetailsAsync(candidate.CandidateKey, ct);

        details.Should().NotBeNull();
        details!.Rows.Should().HaveCount(2);
        details.Rows.Should().Contain(x => x.ResolvedParticipantId == linkedRowId && x.IsLinkedToCanonical);
        details.Rows.Should().Contain(x => !x.IsLinkedToCanonical);
        details.Frequencies.Should().BeEquivalentTo(["145.1000", "402.0000"]);
        details.Divisions.Should().BeEquivalentTo(["336 мсп"]);
        details.HasCanonicalPerson.Should().BeTrue();
    }

    [Fact]
    public async Task GetCandidateDetailsAsync_ReturnsWarning_WhenSameNameLinkedToDifferentCanonicalPersons()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        string candidateKey;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var first = ResolvedParticipant.Create("ЯСТРУБ", "operator", division: "336 мсп");
            var second = ResolvedParticipant.Create("Яструб", "operator", division: "19 омбр");

            db.ResolvedParticipants.AddRange(first, second);

            var canonicalA = CanonicalPerson.Create("ЯСТРУБ / 336");
            canonicalA.AddMember(first.Id);

            var canonicalB = CanonicalPerson.Create("ЯСТРУБ / 19");
            canonicalB.AddMember(second.Id);

            db.CanonicalPersons.AddRange(canonicalA, canonicalB);
            await db.SaveChangesAsync(ct);

            candidateKey = NormalizeCandidateKey("Яструб");
        }

        var details = await service.GetCandidateDetailsAsync(candidateKey, ct);

        details.Should().NotBeNull();
        details!.Warning.Should().NotBeNullOrWhiteSpace();
        details.Warning.Should().Contain("більше одного окремого профілю");
        details.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateCanonicalAsync_CreatesCanonical_AndLinksSelectedRows()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        Guid firstId;
        Guid secondId;
        string candidateKey;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var first = ResolvedParticipant.Create("ШАПКА", "operator", role: "командир", division: "336 мсп");
            var second = ResolvedParticipant.Create("Шапка", "operator", role: "командир", division: "336 мсп");

            firstId = first.Id;
            secondId = second.Id;
            candidateKey = NormalizeCandidateKey(first.Name);

            db.ResolvedParticipants.AddRange(first, second);
            db.InterceptionMessages.AddRange(
                CreateMessage(action, new DateTime(2026, 04, 08, 10, 0, 0, DateTimeKind.Utc), "402.0000", "336 мсп", "ШАПКА"),
                CreateMessage(action, new DateTime(2026, 04, 08, 10, 5, 0, DateTimeKind.Utc), "145.1000", "336 мсп", "Шапка"));

            await db.SaveChangesAsync(ct);
        }

        var details = await service.CreateCanonicalAsync(
            candidateKey,
            [firstId, secondId],
            "ШАПКА",
            "це одна людина",
            ct);

        details.HasCanonicalPerson.Should().BeTrue();
        details.CanonicalPersonId.Should().NotBeNull();
        details.CanonicalNote.Should().Be("це одна людина");
        details.Rows.Should().OnlyContain(x => x.IsLinkedToCanonical);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.CanonicalPersons.Should().HaveCount(1);
            db.CanonicalPersonMembers.Should().HaveCount(2);

            var canonical = await db.CanonicalPersons
                .Include(x => x.Members)
                .SingleAsync(ct);

            canonical.DisplayName.Should().Be("ШАПКА");
            canonical.Note.Should().Be("це одна людина");
            canonical.Members.Select(x => x.ResolvedParticipantId)
                .Should().BeEquivalentTo([firstId, secondId]);
        }
    }

    [Fact]
    public async Task CreateCanonicalAsync_WhenSelectedRowsIncludeSingletonProfile_ReplacesItWithMergedProfile()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        Guid firstId;
        Guid secondId;
        Guid oldCanonicalId;
        string candidateKey;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var first = ResolvedParticipant.Create("ШАПКА", "operator", division: "336 мсп");
            var second = ResolvedParticipant.Create("Шапка", "operator", division: "336 мсп");

            firstId = first.Id;
            secondId = second.Id;
            candidateKey = NormalizeCandidateKey(first.Name);

            db.ResolvedParticipants.AddRange(first, second);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(first.Id);
            db.CanonicalPersons.Add(canonical);

            await db.SaveChangesAsync(ct);
            oldCanonicalId = canonical.Id;
        }

        var details = await service.CreateCanonicalAsync(
            candidateKey,
            [firstId, secondId],
            "ШАПКА",
            null,
            ct);

        details.HasCanonicalPerson.Should().BeTrue();
        details.CanonicalPersonId.Should().NotBeNull();
        details.CanonicalPersonId.Should().NotBe(oldCanonicalId);
        details.Rows.Should().HaveCount(2);
        details.Rows.Should().OnlyContain(x => x.IsLinkedToCanonical);

        await using (var verifyDb = await factory.CreateDbContextAsync(ct))
        {
            verifyDb.CanonicalPersons.Should().HaveCount(1, "старий singleton-профіль має бути замінений новим об'єднаним профілем");
            verifyDb.CanonicalPersonMembers.Should().HaveCount(2);

            var canonical = await verifyDb.CanonicalPersons
                .Include(x => x.Members)
                .SingleAsync(ct);

            canonical.Id.Should().NotBe(oldCanonicalId);
            canonical.Members.Select(x => x.ResolvedParticipantId)
                .Should().BeEquivalentTo([firstId, secondId]);
        }
    }

    [Fact]
    public async Task AttachToCanonicalAsync_AddsNewRows_AndUpdatesNote_WithoutDuplicatingExistingMembers()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        Guid canonicalId;
        Guid existingRowId;
        Guid newRowId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var existing = ResolvedParticipant.Create("СОКІЛ", "operator", role: "командир", division: "336 мсп");
            var incoming = ResolvedParticipant.Create("Сокіл", "operator", role: "командир", division: "336 мсп");

            existingRowId = existing.Id;
            newRowId = incoming.Id;

            db.ResolvedParticipants.AddRange(existing, incoming);

            var canonical = CanonicalPerson.Create("СОКІЛ", "початкова нотатка");
            canonical.AddMember(existing.Id);
            db.CanonicalPersons.Add(canonical);

            await db.SaveChangesAsync(ct);
            canonicalId = canonical.Id;
        }

        var details = await service.AttachToCanonicalAsync(
            canonicalId,
            [existingRowId, newRowId],
            "оновлена нотатка",
            ct);

        details.HasCanonicalPerson.Should().BeTrue();
        details.CanonicalPersonId.Should().Be(canonicalId);
        details.CanonicalNote.Should().Be("оновлена нотатка");
        details.Rows.Should().HaveCount(2);
        details.Rows.Should().OnlyContain(x => x.IsLinkedToCanonical);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var canonical = await db.CanonicalPersons
                .Include(x => x.Members)
                .SingleAsync(x => x.Id == canonicalId, ct);

            canonical.Note.Should().Be("оновлена нотатка");
            canonical.Members.Should().HaveCount(2, "існуючий член не повинен дублюватися");
            canonical.Members.Select(x => x.ResolvedParticipantId)
                .Should().BeEquivalentTo([existingRowId, newRowId]);
        }
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedAtUtc,
        string? frequency,
        string? division,
        string participantName)
    {
        var message = InterceptionMessage.Create(
            observedAtUtc,
            frequency,
            division,
            vectorSignal: "р-н Шевченко",
            interceptionAction: action,
            note: null,
            createdBy: "seed");

        message.AddParticipant(participantName, isUnknown: false, role: "оператор", ordinal: 1);
        return message;
    }

    private static string NormalizeCandidateKey(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value)?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed class TestDbFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        private TestDbFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public static IDbContextFactory<AppDbContext> CreateFactory()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"canonical-person-tests-{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .Options;

            return new TestDbFactory(options);
        }

        public AppDbContext CreateDbContext()
            => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
