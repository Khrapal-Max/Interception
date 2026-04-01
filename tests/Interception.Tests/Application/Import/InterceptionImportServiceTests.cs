//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using FluentAssertions;
using Interception.UI.Application.Import.Services;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Import;

public sealed class InterceptionImportServiceTests
{
    private static InterceptionImportService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task ImportAsync_ValidRows_ImportsMessagesAndParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.InterceptionActions.Add(InterceptionAction.Create("координація дій", ""));
            await db.SaveChangesAsync(ct);
        }

        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = "2026-03-28";
            ws.Cell(2, 2).Value = "10:30";
            ws.Cell(2, 3).Value = "157.0250";
            ws.Cell(2, 4).Value = "656 мсп";
            ws.Cell(2, 7).Value = "ШАПКА";
            ws.Cell(2, 8).Value = "центр";
            ws.Cell(2, 9).Value = "ВОЛГА";
            ws.Cell(2, 10).Value = "оператор";
            ws.Cell(2, 11).Value = "координація дій";
            ws.Cell(2, 12).Value = "імпорт тест";
        });

        var result = await service.ImportAsync(stream, "operator", ct);

        result.ImportedCount.Should().Be(1);
        result.SkippedCount.Should().Be(0);

        await using var checkDb = await factory.CreateDbContextAsync(ct);
        var messages = await checkDb.InterceptionMessages
            .Include(x => x.Participants)
            .ToListAsync(ct);

        messages.Should().ContainSingle();
        messages[0].Participants.Should().HaveCount(2);
        messages[0].Participants.Select(x => x.Name).Should().BeEquivalentTo(["ШАПКА", "ВОЛГА"]);
    }

    [Fact]
    public async Task ImportAsync_ActionNotFound_SkipsRowAndReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = "2026-03-28";
            ws.Cell(2, 2).Value = "10:30";
            ws.Cell(2, 11).Value = "невідома дія";
        });

        var result = await service.ImportAsync(stream, "operator", ct);

        result.ImportedCount.Should().Be(0);
        result.SkippedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Contain("не знайдено в довіднику");

        await using var checkDb = await factory.CreateDbContextAsync(ct);
        (await checkDb.InterceptionMessages.CountAsync(ct)).Should().Be(0);
    }

    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> fill)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Імпорт");
            FillHeaders(sheet);
            fill(sheet);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static void FillHeaders(IXLWorksheet ws)
    {
        ws.Cell(1, 1).Value = "Дата";
        ws.Cell(1, 2).Value = "Час";
        ws.Cell(1, 3).Value = "Частота";
        ws.Cell(1, 4).Value = "Р/М";
        ws.Cell(1, 5).Value = "Точка";
        ws.Cell(1, 6).Value = "Вектор";
        ws.Cell(1, 7).Value = "Ініціатор";
        ws.Cell(1, 8).Value = "Роль ініціатора";
        ws.Cell(1, 9).Value = "Відповідач";
        ws.Cell(1, 10).Value = "Роль відповідача";
        ws.Cell(1, 11).Value = "Дія";
        ws.Cell(1, 12).Value = "Деталі";
    }
}
