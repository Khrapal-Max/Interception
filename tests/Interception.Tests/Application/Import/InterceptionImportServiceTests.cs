//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using FluentAssertions;
using Interception.UI.Application.Import.Services;
using Interception.UI.Domain.Entities;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Import;

public sealed class InterceptionImportServiceTests
{
    private static InterceptionImportService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task ImportAsync_ExportSheetFormat_ImportsMessageParticipantsAndLabels()
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
            ws.Cell(2, 1).Value = new DateTime(2026, 3, 28, 10, 30, 0);
            ws.Cell(2, 2).Value = "157.0250";
            ws.Cell(2, 3).Value = "656 мсп";
            ws.Cell(2, 4).Value = "олексіївка";
            ws.Cell(2, 5).Value = "степове";
            ws.Cell(2, 6).Value = "координація дій";
            ws.Cell(2, 7).Value = "імпорт тест";
            ws.Cell(2, 8).Value = "ШАПКА (центр), ВОЛГА (оператор), НВ 1";
            ws.Cell(2, 9).Value = 3;
            ws.Cell(2, 10).Value = "мітка1, мітка2";
            ws.Cell(2, 11).Value = 2;
        });

        var result = await service.ImportAsync(stream, "operator", ct);

        result.ImportedCount.Should().Be(1);
        result.SkippedCount.Should().Be(0);

        await using var checkDb = await factory.CreateDbContextAsync(ct);
        var messages = await checkDb.InterceptionMessages
            .Include(x => x.Participants)
            .Include(x => x.Labels)
            .ToListAsync(ct);

        messages.Should().ContainSingle();

        var message = messages[0];
        message.Frequency.Should().Be("157.0250");
        message.Division.Should().Be("656 мсп");
        message.VectorSignal.Should().Be("олексіївка");
        message.PointSignal.Should().Be("степове");
        message.Note.Should().Be("імпорт тест");

        message.Participants.Should().HaveCount(3);
        message.Participants.Should().Contain(x => x.Name == "ШАПКА" && x.Role == "центр" && !x.IsUnknown);
        message.Participants.Should().Contain(x => x.Name == "ВОЛГА" && x.Role == "оператор" && !x.IsUnknown);
        message.Participants.Should().Contain(x => x.IsUnknown);

        message.Labels.Select(x => x.NameLabel).Should().BeEquivalentTo(["мітка1", "мітка2"]);
    }

    [Fact]
    public async Task ImportAsync_ActionNotFound_SkipsRowAndReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = new DateTime(2026, 3, 28, 10, 30, 0);
            ws.Cell(2, 6).Value = "невідома дія";
            ws.Cell(2, 8).Value = "ШАПКА (центр)";
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
            var sheet = workbook.AddWorksheet("Спостереження");
            FillHeaders(sheet);
            fill(sheet);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static void FillHeaders(IXLWorksheet ws)
    {
        ws.Cell(1, 1).Value = "Дата/час";
        ws.Cell(1, 2).Value = "Частота";
        ws.Cell(1, 3).Value = "Підрозділ";
        ws.Cell(1, 4).Value = "Вектор";
        ws.Cell(1, 5).Value = "Точка";
        ws.Cell(1, 6).Value = "Дія";
        ws.Cell(1, 7).Value = "Примітка";
        ws.Cell(1, 8).Value = "Учасники";
        ws.Cell(1, 9).Value = "Кількість учасників";
        ws.Cell(1, 10).Value = "Мітки";
        ws.Cell(1, 11).Value = "Кількість міток";
        ws.Cell(1, 12).Value = "Створив";
        ws.Cell(1, 13).Value = "Створено";
        ws.Cell(1, 14).Value = "Оновлено";
    }
}
