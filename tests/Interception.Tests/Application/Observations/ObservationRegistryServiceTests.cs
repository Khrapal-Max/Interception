//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Observations.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Application.Observations;

public sealed class ObservationRegistryServiceTests
{
    [Fact]
    public async Task SearchAsync_FiltersByPersonLabel()
    {
        var factory = TestDbFactory.CreateFactory();

        await using (var seed = factory.CreateDbContext())
        {
            var o1 = Observation.Create(new DateOnly(2026, 3, 1), DayPart.FirstHalf, "Перевезення", locationRaw: "Степове");
            o1.AddParticipant("КЛИМ", isUnknown: false, ordinal: 1);
            o1.AddParticipant("НВ", isUnknown: true, ordinal: 2);

            var o2 = Observation.Create(new DateOnly(2026, 3, 2), DayPart.SecondHalf, "Зустріч", locationRaw: "Київ");
            o2.AddParticipant("ТАШКЕНТ", isUnknown: false, ordinal: 1);
            o2.AddParticipant("КЛИМ", isUnknown: false, ordinal: 2);

            seed.AddRange(o1, o2);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationRegistryService(factory);

        var page = await svc.SearchAsync(new ObservationRegistryFilter
        {
            Person = "клим",
            Take = 50
        }, CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, x => Assert.Contains(x.Participants, p => string.Equals(p.LabelRaw, "КЛИМ", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task SearchAsync_PersonQueryNV_ReturnsObservationsWithUnknownParticipants()
    {
        var factory = TestDbFactory.CreateFactory();

        Guid o2Id;

        await using (var seed = factory.CreateDbContext())
        {
            var o1 = Observation.Create(new DateOnly(2026, 3, 1), DayPart.FirstHalf, "Перевезення");
            o1.AddParticipant("КЛИМ", isUnknown: false, ordinal: 1);
            o1.AddParticipant(null, isUnknown: true, ordinal: 2); // unknown

            var o2 = Observation.Create(new DateOnly(2026, 3, 2), DayPart.SecondHalf, "Зустріч");
            o2.AddParticipant("ТАШКЕНТ", isUnknown: false, ordinal: 1);
            o2Id = o2.Id;

            var o3 = Observation.Create(new DateOnly(2026, 3, 3), DayPart.FirstHalf, "Спостереження");
            o3.AddParticipant("НВ", isUnknown: true, ordinal: 1);

            seed.AddRange(o1, o2, o3);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationRegistryService(factory);

        var page = await svc.SearchAsync(new ObservationRegistryFilter
        {
            Person = "НВ",
            Take = 50
        }, CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.DoesNotContain(page.Items, x => x.Id == o2Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByDateRangeAndDayPart()
    {
        var factory = TestDbFactory.CreateFactory();

        Guid o3Id;

        await using (var seed = factory.CreateDbContext())
        {
            var o1 = Observation.Create(new DateOnly(2026, 3, 1), DayPart.FirstHalf, "A");
            var o2 = Observation.Create(new DateOnly(2026, 3, 2), DayPart.FirstHalf, "B");
            var o3 = Observation.Create(new DateOnly(2026, 3, 2), DayPart.SecondHalf, "C");
            var o4 = Observation.Create(new DateOnly(2026, 3, 3), DayPart.FirstHalf, "D");
            o3Id = o3.Id;

            seed.AddRange(o1, o2, o3, o4);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationRegistryService(factory);

        var page = await svc.SearchAsync(new ObservationRegistryFilter
        {
            DateFrom = new DateOnly(2026, 3, 2),
            DateTo = new DateOnly(2026, 3, 2),
            DayPart = (short)DayPart.SecondHalf,
            Take = 50
        }, CancellationToken.None);

        Assert.Equal(1, page.Total);
        Assert.Equal(o3Id, page.Items.Single().Id);
    }

    [Fact]
    public async Task SearchAsync_OrdersByDateThenDayPartDescending()
    {
        var factory = TestDbFactory.CreateFactory();

        Guid olderId;
        Guid sameDateFirstId;
        Guid sameDateSecondId;

        await using (var seed = factory.CreateDbContext())
        {
            var d = new DateOnly(2026, 3, 3);

            var older = Observation.Create(new DateOnly(2026, 3, 1), DayPart.SecondHalf, "Old");
            var sameDateFirst = Observation.Create(d, DayPart.FirstHalf, "SameDateFirst");
            var sameDateSecond = Observation.Create(d, DayPart.SecondHalf, "SameDateSecond");

            olderId = older.Id;
            sameDateFirstId = sameDateFirst.Id;
            sameDateSecondId = sameDateSecond.Id;

            seed.AddRange(older, sameDateFirst, sameDateSecond);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationRegistryService(factory);

        var page = await svc.SearchAsync(new ObservationRegistryFilter { Take = 10 }, CancellationToken.None);

        Assert.Equal(3, page.Total);
        Assert.Equal(sameDateSecondId, page.Items[0].Id); // SecondHalf should come first
        Assert.Equal(sameDateFirstId, page.Items[1].Id);
        Assert.Equal(olderId, page.Items[2].Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsParticipantsOrderedByOrdinal()
    {
        var factory = TestDbFactory.CreateFactory();

        Guid id;

        await using (var seed = factory.CreateDbContext())
        {
            var o = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "Test");
            o.AddParticipant("A", isUnknown: false, ordinal: 2);
            o.AddParticipant("B", isUnknown: false, ordinal: 1);
            id = o.Id;

            seed.Add(o);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationRegistryService(factory);

        var dto = await svc.GetByIdAsync(id, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(id, dto!.Id);
        Assert.Equal(2, dto.Participants.Count);
        Assert.Equal("B", dto.Participants[0].LabelRaw);
        Assert.Equal(1, dto.Participants[0].Ordinal);
        Assert.Equal("A", dto.Participants[1].LabelRaw);
        Assert.Equal(2, dto.Participants[1].Ordinal);
    }
}

