//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.TextBlock;

namespace Interception.Tests.Application.Interceptions.Services.TextBlock;

public sealed class TextBlockParserTests
{
    // -------------------------------------------------------------------------
    // Повний блок — еталонний приклад
    // -------------------------------------------------------------------------

    private const string FullBlock = """
        21.03.2026, 09:15:50
        410.1370
        УКХ р/м ім. 3 кулб 69 обрп (р-н Маліївка - Січневе - Воскресенка)
        ЗВЕЗДА
        ЦЫГАН, ПАНДА
        — Цыган, я Звезда, прием.
        — Да, Цыган принял.
        Коментар: йм.наказ на ураження
        """;

    [Fact]
    public void Parse_FullBlock_ExtractsAllFields()
    {
        var result = TextBlockParser.Parse(FullBlock);

        result.IsSuccess.Should().BeTrue();
        result.ObservedDate.Should().Be(new DateTime(2026, 3, 21, 9, 15, 50));
        result.Frequency.Should().Be("410.1370");
        result.Division.Should().Be("УКХ р/м ім. 3 кулб 69 обрп");
        result.VectorSignal.Should().Be("р-н Маліївка - Січневе - Воскресенка");
        result.Initiator.Should().Be("ЗВЕЗДА");
        result.Responders.Should().BeEquivalentTo(new[] { "ЦЫГАН", "ПАНДА" });
        result.Note.Should().Contain("Цыган, я Звезда");
        result.Note.Should().Contain("Цыган принял");
    }

    [Fact]
    public void Parse_FullBlock_CommentIsIgnored()
    {
        var result = TextBlockParser.Parse(FullBlock);

        result.Note.Should().NotContain("Коментар");
        result.Note.Should().NotContain("наказ на ураження");
    }

    // -------------------------------------------------------------------------
    // Дата та час
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("21.03.2026, 09:15:50\n410.1370\nДивізія\nЗВЕЗДА")]
    [InlineData("21.03.2026 09:15\n410.1370\nДивізія\nЗВЕЗДА")]
    [InlineData("21/03/2026, 09:15\n410.1370\nДивізія\nЗВЕЗДА")]
    public void Parse_VariousDateFormats_ParsedCorrectly(string text)
    {
        var result = TextBlockParser.Parse(text);

        result.IsSuccess.Should().BeTrue();
        result.ObservedDate.Should().NotBeNull();
        result.ObservedDate!.Value.Day.Should().Be(21);
        result.ObservedDate.Value.Month.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // Частота
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("410.1370", "410.1370")]
    [InlineData("150.725", "150.725")]
    [InlineData("410,1370", "410.1370")] // кома → крапка
    public void Parse_Frequency_NormalizedCorrectly(string freqLine, string expected)
    {
        var text = $"21.03.2026, 09:15\n{freqLine}\nДивізія\nЗВЕЗДА";
        var result = TextBlockParser.Parse(text);

        result.Frequency.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Division і VectorSignal
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DivisionWithVector_SplitsCorrectly()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб 656 мсп (степове-олексіївка)
            ШАПКА
            """;

        var result = TextBlockParser.Parse(text);

        result.Division.Should().Be("1 мсб 656 мсп");
        result.VectorSignal.Should().Be("степове-олексіївка");
    }

    [Fact]
    public void Parse_DivisionWithoutVector_DivisionOnly()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб 656 мсп
            ШАПКА
            """;

        var result = TextBlockParser.Parse(text);

        result.Division.Should().Be("1 мсб 656 мсп");
        result.VectorSignal.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Учасники
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_MultipleRespondersCommaSeparated()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            ЗВЕЗДА
            ЦЫГАН, ПАНДА, РУБІКОН
            — діалог
            """;

        var result = TextBlockParser.Parse(text);

        result.Initiator.Should().Be("ЗВЕЗДА");
        result.Responders.Should().BeEquivalentTo(new[] { "ЦЫГАН", "ПАНДА", "РУБІКОН" });
    }

    [Fact]
    public void Parse_MultipleRespondersNewLines()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            ЗВЕЗДА
            ЦЫГАН
            ПАНДА
            — діалог
            """;

        var result = TextBlockParser.Parse(text);

        result.Initiator.Should().Be("ЗВЕЗДА");
        result.Responders.Should().BeEquivalentTo(new[] { "ЦЫГАН", "ПАНДА" });
    }

    [Fact]
    public void Parse_NvInitiator_IsNullInitiatorWithResponder()
    {
        // НВ резервує позицію ініціатора як null, ЦЫГАН стає відповідачем
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            НВ
            ЦЫГАН
            """;

        var result = TextBlockParser.Parse(text);

        result.IsSuccess.Should().BeTrue();
        result.Initiator.Should().BeNull();
        result.Responders.Should().ContainSingle().Which.Should().Be("ЦЫГАН");
    }

    [Fact]
    public void Parse_NvAndKnownOnSameLine_NullInitiatorKnownResponder()
    {
        // "НВ, ЦЫГАН" → ініціатор = null, відповідач = ЦЫГАН
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            НВ, ЦЫГАН
            — діалог
            """;

        var result = TextBlockParser.Parse(text);

        result.Initiator.Should().BeNull();
        result.Responders.Should().ContainSingle().Which.Should().Be("ЦЫГАН");
    }

    [Fact]
    public void Parse_BothNv_BothNull()
    {
        // Обидва НВ → ініціатор і відповідач = null
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            НВ
            НВ
            — діалог
            """;

        var result = TextBlockParser.Parse(text);

        result.Initiator.Should().BeNull();
        result.Responders.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public void Parse_SingleParticipant_InitiatorOnly()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            ЗВЕЗДА
            """;

        var result = TextBlockParser.Parse(text);

        result.Initiator.Should().Be("ЗВЕЗДА");
        result.Responders.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Note (діалог)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DialogLines_GoToNote()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            ЗВЕЗДА
            — Перший рядок діалогу.
            — Другий рядок діалогу.
            """;

        var result = TextBlockParser.Parse(text);

        result.Note.Should().Contain("Перший рядок діалогу");
        result.Note.Should().Contain("Другий рядок діалогу");
    }

    [Fact]
    public void Parse_NoDialog_NoteIsNull()
    {
        const string text = """
            21.03.2026, 09:15
            157.0250
            1 мсб
            ЗВЕЗДА
            """;

        var result = TextBlockParser.Parse(text);

        result.Note.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Помилкові випадки
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_EmptyText_ReturnsFail()
    {
        var result = TextBlockParser.Parse("");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Parse_NullText_ReturnsFail()
    {
        var result = TextBlockParser.Parse(null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Parse_OnlyOneLine_ReturnsFail()
    {
        var result = TextBlockParser.Parse("21.03.2026, 09:15");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Parse_ArbitraryText_NoDateNoFrequency_AllFieldsNull()
    {
        // Парсер не кидає помилку на довільний текст — він просто
        // не розпізнає структурованих полів. Date і Frequency = null,
        // Division може заповнитись першим рядком (не схожим на позивний).
        // IsSuccess = true, оператор бачить що поля порожні і коригує вручну.
        var result = TextBlockParser.Parse("рядок один\nрядок два\nрядок три");

        result.ObservedDate.Should().BeNull();
        result.Frequency.Should().BeNull();
        result.Initiator.Should().BeNull();
    }
}
