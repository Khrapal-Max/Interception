//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Services;
using Interception.UI.Domain.Entities;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions;

/// <summary>
/// Тести для сервісу ручного ведення фактів структурного керування.
/// </summary>
public sealed class PersonDirectiveRelationServiceTests
{
    private static PersonDirectiveRelationService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task GetIdentityOptionsAsync_WithoutFilter_ReturnsCanonicalAndStandaloneResolved()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var canonicalResolved = ResolvedParticipant.Create("ШАПКА", "seed", "координатор", "336 мсп");
            var standaloneResolved = ResolvedParticipant.Create("ГРОМ", "seed", "оператор", "19 омбр");
            var secondStandalone = ResolvedParticipant.Create("ВОЛГА", "seed", "зв'язок", "145.1000");

            db.ResolvedParticipants.AddRange(canonicalResolved, standaloneResolved, secondStandalone);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(canonicalResolved.Id);
            db.CanonicalPersons.Add(canonical);

            await db.SaveChangesAsync(ct);
        }

        var options = await service.GetIdentityOptionsAsync(null, ct);

        options.Should().Contain(x => x.IsCanonicalPerson && x.DisplayName == "ШАПКА");
        options.Should().Contain(x => !x.IsCanonicalPerson && x.DisplayName.Contains("ГРОМ"));
        options.Should().Contain(x => !x.IsCanonicalPerson && x.DisplayName.Contains("ВОЛГА"));
        options.Should().NotContain(x => !x.IsCanonicalPerson && x.DisplayName.Contains("ШАПКА"),
            "resolved, already linked to canonical profile, should not be returned as standalone");
    }

    [Fact]
    public async Task GetIdentityOptionsAsync_WithParticipantFilter_ReturnsOnlyMatchingCanonicalAndStandaloneResolved()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var canonicalResolved = ResolvedParticipant.Create("ШАПКА", "seed", "координатор", "336 мсп");
            var standaloneResolved = ResolvedParticipant.Create("ГРОМ", "seed", "оператор", "19 омбр");
            var unrelatedResolved = ResolvedParticipant.Create("БАРС", "seed", "оператор", "5 ошб");

            db.ResolvedParticipants.AddRange(canonicalResolved, standaloneResolved, unrelatedResolved);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(canonicalResolved.Id);
            db.CanonicalPersons.Add(canonical);

            await db.SaveChangesAsync(ct);
        }

        var options = await service.GetIdentityOptionsAsync(["шапка", "гром"], ct);

        options.Should().Contain(x => x.IsCanonicalPerson && x.DisplayName == "ШАПКА");
        options.Should().Contain(x => !x.IsCanonicalPerson && x.DisplayName.Contains("ГРОМ"));
        options.Should().NotContain(x => x.DisplayName.Contains("БАРС"));
    }

    [Fact]
    public async Task SaveAsync_CreatesRelation_AndGetAllAsync_ReturnsDisplayNames()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        Guid canonicalId;
        Guid resolvedId;
        Guid observationId = Guid.NewGuid();

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var canonicalResolved = ResolvedParticipant.Create("ШАПКА", "seed", "координатор", "336 мсп");
            var standaloneResolved = ResolvedParticipant.Create("ГРОМ", "seed", "оператор", "19 омбр");

            db.ResolvedParticipants.AddRange(canonicalResolved, standaloneResolved);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(canonicalResolved.Id);
            db.CanonicalPersons.Add(canonical);

            await db.SaveChangesAsync(ct);

            canonicalId = canonical.Id;
            resolvedId = standaloneResolved.Id;
        }

        await service.SaveAsync(new PersonDirectiveRelationSaveDto
        {
            FromCanonicalPersonId = canonicalId,
            ToResolvedParticipantId = resolvedId,
            RelationType = DirectiveRelationTypeDto.Command,
            Confidence = DirectiveRelationConfidenceDto.High,
            SourceObservationId = observationId,
            IsManual = true,
            Comment = "Явний наказ"
        }, ct);

        var items = await service.GetAllAsync(ct);

        items.Should().ContainSingle();
        var item = items.Single();

        item.FromCanonicalPersonId.Should().Be(canonicalId);
        item.ToResolvedParticipantId.Should().Be(resolvedId);
        item.FromDisplayName.Should().Be("ШАПКА");
        item.ToDisplayName.Should().Contain("ГРОМ");
        item.RelationType.Should().Be(DirectiveRelationTypeDto.Command);
        item.Confidence.Should().Be(DirectiveRelationConfidenceDto.High);
        item.SourceObservationId.Should().Be(observationId);
        item.IsManual.Should().BeTrue();
        item.Comment.Should().Be("Явний наказ");
    }

    [Fact]
    public async Task SaveAsync_WhenSameEndpointsExist_UpdatesExistingRelation()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        Guid leftId;
        Guid rightId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var left = ResolvedParticipant.Create("ШАПКА", "seed", "координатор", "336 мсп");
            var right = ResolvedParticipant.Create("ГРОМ", "seed", "оператор", "19 омбр");

            db.ResolvedParticipants.AddRange(left, right);
            await db.SaveChangesAsync(ct);

            leftId = left.Id;
            rightId = right.Id;
        }

        await service.SaveAsync(new PersonDirectiveRelationSaveDto
        {
            FromResolvedParticipantId = leftId,
            ToResolvedParticipantId = rightId,
            RelationType = DirectiveRelationTypeDto.Command,
            Confidence = DirectiveRelationConfidenceDto.High,
            IsManual = true,
            Comment = "перше збереження"
        }, ct);

        await service.SaveAsync(new PersonDirectiveRelationSaveDto
        {
            FromResolvedParticipantId = leftId,
            ToResolvedParticipantId = rightId,
            RelationType = DirectiveRelationTypeDto.Control,
            Confidence = DirectiveRelationConfidenceDto.Medium,
            IsManual = true,
            Comment = "оновлено"
        }, ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var rows = await verifyDb.PersonDirectiveRelations.ToListAsync(ct);

        rows.Should().ContainSingle();
        rows[0].RelationType.Should().Be(DirectiveRelationType.Control);
        rows[0].Confidence.Should().Be(DirectiveRelationConfidence.Medium);
        rows[0].Comment.Should().Be("оновлено");
    }

    [Fact]
    public async Task SaveAsync_WhenSameSourceAndTarget_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        Guid resolvedId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var resolved = ResolvedParticipant.Create("ШАПКА", "seed", "координатор", "336 мсп");
            db.ResolvedParticipants.Add(resolved);
            await db.SaveChangesAsync(ct);
            resolvedId = resolved.Id;
        }

        var act = () => service.SaveAsync(new PersonDirectiveRelationSaveDto
        {
            FromResolvedParticipantId = resolvedId,
            ToResolvedParticipantId = resolvedId,
            RelationType = DirectiveRelationTypeDto.Command,
            Confidence = DirectiveRelationConfidenceDto.High,
            IsManual = true
        }, ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*самою собою*");
    }

    [Fact]
    public async Task DeleteAsync_RemovesRelation()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        Guid relationId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var left = ResolvedParticipant.Create("ШАПКА", "seed", "координатор", "336 мсп");
            var right = ResolvedParticipant.Create("ГРОМ", "seed", "оператор", "19 омбр");

            db.ResolvedParticipants.AddRange(left, right);
            await db.SaveChangesAsync(ct);

            var relation = PersonDirectiveRelation.Create(
                fromCanonicalPersonId: null,
                fromResolvedParticipantId: left.Id,
                toCanonicalPersonId: null,
                toResolvedParticipantId: right.Id,
                relationType: DirectiveRelationType.Command,
                confidence: DirectiveRelationConfidence.High,
                sourceObservationId: null,
                isManual: true,
                comment: "видалити");

            db.PersonDirectiveRelations.Add(relation);
            await db.SaveChangesAsync(ct);

            relationId = relation.Id;
        }

        await service.DeleteAsync(relationId, ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        (await verifyDb.PersonDirectiveRelations.CountAsync(ct)).Should().Be(0);
    }

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
                .UseInMemoryDatabase($"directive-relations-tests-{Guid.NewGuid()}")
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
