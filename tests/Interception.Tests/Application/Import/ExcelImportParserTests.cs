//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using FluentAssertions;
using Interception.Application.Import.Services;

namespace Interception.Tests.Application.Import;

public sealed class ExcelImportParserTests
{
    [Fact]
    public void Parse_ValidRow_NormalizesUnknownMarkersAndTrimmedValues()
    {
        var parser = new ExcelImportParser();
        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = new DateTime(2026, 3, 28);
            ws.Cell(2, 2).Value = new TimeSpan(10, 15, 0);
            ws.Cell(2, 3).Value = " 157.0250 ";
            ws.Cell(2, 4).Value = " 656 мсп ";
            ws.Cell(2, 5).Value = " степове ";
            ws.Cell(2, 6).Value = " олексіївка ";
            ws.Cell(2, 7).Value = " НВ ";
            ws.Cell(2, 8).Value = "  центр ";
            ws.Cell(2, 9).Value = "  ВОЛГА ";
            ws.Cell(2, 10).Value = " оператор  ";
            ws.Cell(2, 11).Value = " координація дій ";
            ws.Cell(2, 12).Value = " test note ";
        });

        var result = ExcelImportParser.Parse(stream);

        result.Should().ContainSingle();
        var (row, error) = result.Single();

        error.Should().BeNull();
        row.Should().NotBeNull();
        row!.Date.Should().Be(new DateOnly(2026, 3, 28));
        row.Time.Should().Be(new TimeOnly(10, 15, 0));
        row.Frequency.Should().Be("157.0250");
        row.Division.Should().Be("656 мсп");
        row.InitiatorName.Should().BeNull("маркер 'НВ' має вважатись невідомим абонентом");
        row.ResponderName.Should().Be("ВОЛГА");
        row.ActionName.Should().Be("координація дій");
    }

    [Fact]
    public void Parse_InvalidDate_ReturnsRowError()
    {
        var parser = new ExcelImportParser();
        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = "не дата";
            ws.Cell(2, 2).Value = "10:30";
            ws.Cell(2, 11).Value = "координація дій";
        });

        var result = ExcelImportParser.Parse(stream);

        result.Should().ContainSingle();
        var (row, error) = result.Single();

        row.Should().BeNull();
        error.Should().NotBeNull();
        error!.RowNumber.Should().Be(2);
        error.Message.Should().Contain("Невірний формат дати");
    }

    [Fact]
    public void Parse_SkipsSheetNamedDiyi_AndUsesFirstDataSheet()
    {
        var parser = new ExcelImportParser();
        using var stream = BuildWorkbookWithActionsSheet();

        var result = ExcelImportParser.Parse(stream);

        var (row, error) = result.Should().ContainSingle().Subject;
        error.Should().BeNull();
        row.Should().NotBeNull();
        row!.ActionName.Should().Be("доповідь");
    }

    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> fill)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Дані");
            FillHeaders(sheet);
            fill(sheet);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildWorkbookWithActionsSheet()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var actions = workbook.AddWorksheet("ДІЇ");
            FillHeaders(actions);
            actions.Cell(2, 1).Value = "невалідно";

            var data = workbook.AddWorksheet("Імпорт");
            FillHeaders(data);
            data.Cell(2, 1).Value = new DateTime(2026, 3, 29);
            data.Cell(2, 2).Value = "11:20";
            data.Cell(2, 11).Value = "доповідь";

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
