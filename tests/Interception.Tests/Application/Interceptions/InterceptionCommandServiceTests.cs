//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Analytics.Dtos;
using Interception.Application.Interceptions.Dtos;
using Interception.Application.Interceptions.Services;
using Interception.Domain.Entities;
using Interception.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions;

public sealed class InterceptionCommandServiceTests
{
    private static InterceptionCommandService CreateService(IDbContextFactory<PostgreSqlDbContext> factory)
        => new(factory);

    private static async Task<InterceptionAction> SeedActionAsync(
        IDbContextFactory<PostgreSqlDbContext> factory,
        string name = "координація дій",
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create(name, "");
        db.InterceptionActions.Add(action);
        await db.SaveChangesAsync(ct);
        return action;
    }

    private static InterceptionFormDto MakeForm(
        Guid actionId,
        string? frequency = "157.0250",
        string? division = "1 мсб 656 мсп",
        string? vectorSignal = "степове-олексіївка",
        string? note = "тестова нотатка",
        DateTime? observedDate = null,
        List<ParticipantFormDto>? participants = null,
        List<string>? labels = null)
    {
        return new InterceptionFormDto
        {
            ObservedDate = observedDate ?? new DateTime(2026, 3, 14, 16, 30, 0, DateTimeKind.Utc),
            Frequency = frequency,
            Division = division,
            VectorSignal = vectorSignal,
            InterceptionActionId = actionId,
            Note = note,
            Participants = participants ??
            [
                new ParticipantFormDto { Ordinal = 1, Name = "ШАПКА", IsUnknown = false, Role = "центр" },
                new ParticipantFormDto { Ordinal = 2, Name = "ВОЛГА", IsUnknown = false, Role = "бпла" },
            ],
            Labels = labels ?? ["тест"],
        };
    }

    [Fact]
    public async Task CreateAsync_ValidForm_PersistsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var message = await svc.CreateAsync(MakeForm(action.Id), "operator", ct);

        message.Id.Should().NotBeEmpty();
        message.Frequency.Should().Be("157.0250");
        message.Division.Should().Be("1 мсб 656 мсп");
        message.Participants.Should().HaveCount(2);
        message.Labels.Should().ContainSingle(l => l.NameLabel == "тест");
    }

    [Fact]
    public async Task CreateAsync_UnknownAction_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.CreateAsync(MakeForm(Guid.NewGuid()), "operator", ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = CreateService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var created = await command.CreateAsync(MakeForm(action.Id), "operator", ct);

        var updateForm = MakeForm(
            actionId: action.Id,
            frequency: "410.1370",
            division: "4 мсб 186 мсп",
            vectorSignal: "новий вектор",
            note: "оновлена нотатка",
            participants:
            [
                new ParticipantFormDto { Ordinal = 1, Name = "КАРАСУК", IsUnknown = false }
            ],
            labels: ["мітка-2"]);

        await command.UpdateAsync(created.Id, updateForm, ct);
        var updated = await query.GetByIdAsync(created.Id, ct);

        updated.Should().NotBeNull();
        updated!.Frequency.Should().Be("410.1370");
        updated.Division.Should().Be("4 мсб 186 мсп");
        updated.Participants.Should().ContainSingle(p => p.Name == "КАРАСУК");
        updated.Labels.Should().ContainSingle(l => l.NameLabel == "мітка-2");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var act = async () => await svc.UpdateAsync(Guid.NewGuid(), MakeForm(action.Id), ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    [Fact]
    public async Task DeleteAsync_RemovesMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = CreateService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var created = await command.CreateAsync(MakeForm(action.Id), "operator", ct);
        await command.DeleteAsync(created.Id, ct);

        var found = await query.GetByIdAsync(created.Id, ct);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.DeleteAsync(Guid.NewGuid(), ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
