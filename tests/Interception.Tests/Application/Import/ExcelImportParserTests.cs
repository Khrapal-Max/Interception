//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using FluentAssertions;
using Interception.UI.Application.Import.Services;

namespace Interception.Tests.Application.Import;

public sealed class ExcelImportParserTests
{
    [Fact]
    public void Parse_ExportSheetRow_ParsesParticipantsUnknownAndTrimmedValues()
    {
        var parser = new ExcelImportParser();
        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = new DateTime(2026, 3, 28, 10, 15, 0);
            ws.Cell(2, 2).Value = " 157.0250 ";
            ws.Cell(2, 3).Value = " 656 мсп ";
            ws.Cell(2, 4).Value = " олексіївка ";
            ws.Cell(2, 5).Value = " степове ";
            ws.Cell(2, 6).Value = " координація дій ";
            ws.Cell(2, 7).Value = " test note ";
            ws.Cell(2, 8).Value = " ШАПКА (центр), ВОЛГА (оператор), НВ 1 ";
            ws.Cell(2, 9).Value = 3;
            ws.Cell(2, 10).Value = " мітка1, мітка2 ";
            ws.Cell(2, 11).Value = 2;
        });

        var result = parser.Parse(stream);

        result.Should().ContainSingle();
        var (row, error) = result.Single();

        error.Should().BeNull();
        row.Should().NotBeNull();

        var rowObj = row!;
        GetProperty<string?>(rowObj, "Frequency").Should().Be("157.0250");
        GetProperty<string?>(rowObj, "Division").Should().Be("656 мсп");
        GetProperty<string?>(rowObj, "VectorSignal", "Vector").Should().Be("олексіївка");
        GetProperty<string?>(rowObj, "PointSignal", "Point").Should().Be("степове");
        GetProperty<string?>(rowObj, "ActionName", "Action").Should().Be("координація дій");
        GetProperty<string?>(rowObj, "Details", "Note").Should().Be("test note");

        var participants = ReadParticipants(rowObj);
        participants.Should().HaveCount(3);
        participants.Should().ContainSingle(x => x.Name == "ШАПКА" && x.Role == "центр" && !x.IsUnknown);
        participants.Should().ContainSingle(x => x.Name == "ВОЛГА" && x.Role == "оператор" && !x.IsUnknown);
        participants.Should().ContainSingle(x => x.IsUnknown);

        var labels = ReadLabels(rowObj);
        labels.Should().BeEquivalentTo(["мітка1", "мітка2"]);
    }

    [Fact]
    public void Parse_InvalidObservedDateTime_ReturnsRowError()
    {
        var parser = new ExcelImportParser();
        using var stream = BuildWorkbook((ws) =>
        {
            ws.Cell(2, 1).Value = "не дата";
            ws.Cell(2, 6).Value = "координація дій";
            ws.Cell(2, 8).Value = "ШАПКА (центр)";
        });

        var result = parser.Parse(stream);

        result.Should().ContainSingle();
        var (row, error) = result.Single();

        row.Should().BeNull();
        error.Should().NotBeNull();
        error!.RowNumber.Should().Be(2);
        error.Message.Should().Contain("дат");
    }

    [Fact]
    public void Parse_UsesSheetNamedSposterezhennya()
    {
        var parser = new ExcelImportParser();
        using var stream = BuildWorkbookWithActionsSheet();

        var result = parser.Parse(stream);

        var (row, error) = result.Should().ContainSingle().Subject;
        error.Should().BeNull();
        row.Should().NotBeNull();
        GetProperty<string?>(row!, "ActionName", "Action").Should().Be("доповідь");
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

    private static MemoryStream BuildWorkbookWithActionsSheet()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var actions = workbook.AddWorksheet("ДІЇ");
            actions.Cell(1, 1).Value = "Дія";
            actions.Cell(2, 1).Value = "невалідно";

            var data = workbook.AddWorksheet("Спостереження");
            FillHeaders(data);
            data.Cell(2, 1).Value = new DateTime(2026, 3, 29, 11, 20, 0);
            data.Cell(2, 6).Value = "доповідь";
            data.Cell(2, 8).Value = "ШАПКА (центр)";

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

    private static T? GetProperty<T>(object instance, params string[] names)
    {
        foreach (var name in names)
        {
            var property = instance.GetType().GetProperty(name);
            if (property is null)
                continue;

            var value = property.GetValue(instance);
            if (value is T typed)
                return typed;

            if (value is null)
                return default;
        }

        return default;
    }

    private static IReadOnlyList<(string? Name, string? Role, bool IsUnknown)> ReadParticipants(object row)
    {
        var participantsProperty = row.GetType().GetProperty("Participants");
        if (participantsProperty?.GetValue(row) is System.Collections.IEnumerable participants)
        {
            var result = new List<(string? Name, string? Role, bool IsUnknown)>();
            foreach (var item in participants)
            {
                var type = item!.GetType();
                result.Add((
                    type.GetProperty("Name")?.GetValue(item) as string,
                    type.GetProperty("Role")?.GetValue(item) as string,
                    type.GetProperty("IsUnknown")?.GetValue(item) as bool? == true));
            }

            return result;
        }

        // fallback для старішого DTO
        return
        [
            (
                GetProperty<string?>(row, "InitiatorName"),
                GetProperty<string?>(row, "InitiatorRole"),
                GetProperty<string?>(row, "InitiatorName") is null
            ),
            (
                GetProperty<string?>(row, "ResponderName"),
                GetProperty<string?>(row, "ResponderRole"),
                GetProperty<string?>(row, "ResponderName") is null
            )
        ];
    }

    private static IReadOnlyList<string> ReadLabels(object row)
    {
        var labelsProperty = row.GetType().GetProperty("Labels");
        if (labelsProperty?.GetValue(row) is System.Collections.IEnumerable labels)
        {
            return labels.Cast<object>()
                .Select(x => x?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();
        }

        return [];
    }
}
