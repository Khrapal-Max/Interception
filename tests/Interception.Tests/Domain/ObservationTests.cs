//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Domain;

public class ObservationTests
{
    [Fact]
    public void Create_Throws_When_Action_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "   "));
    }

    [Fact]
    public void Create_Normalizes_Action()
    {
        var obs = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "  Перевезення  ");

        Assert.Equal("Перевезення", obs.ActionRaw);
        Assert.Equal("перевезення", obs.ActionNorm);
    }

    [Fact]
    public void AddParticipant_Prevents_Duplicates_By_Normalized_Label()
    {
        var obs = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "дія");

        obs.AddParticipant("КЛИМ", isUnknown: false);

        Assert.Throws<InvalidOperationException>(() =>
            obs.AddParticipant("  клим ", isUnknown: false));
    }

    [Fact]
    public void AddParticipant_Marks_Unknown_When_Label_Missing()
    {
        var obs = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "дія");

        var p = obs.AddParticipant(labelRaw: null, isUnknown: false);

        Assert.True(p.IsUnknown);
        Assert.Null(p.LabelRaw);
        Assert.Null(p.LabelNorm);
    }

    [Fact]
    public void ContentHash_Changes_When_Participants_Change()
    {
        var obs = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "дія");
        var h1 = obs.ContentHash;

        obs.AddParticipant("КЛИМ", isUnknown: false);
        var h2 = obs.ContentHash;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void ContentHash_Is_Stable_For_Same_Participants_Order_By_Ordinal()
    {
        var o1 = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "дія");
        o1.AddParticipant("A", isUnknown: false, ordinal: 2);
        o1.AddParticipant("B", isUnknown: false, ordinal: 1);

        var o2 = Observation.Create(new DateOnly(2026, 3, 4), DayPart.FirstHalf, "дія");
        o2.AddParticipant("B", isUnknown: false, ordinal: 1);
        o2.AddParticipant("A", isUnknown: false, ordinal: 2);

        Assert.Equal(o1.ContentHash, o2.ContentHash);
    }
}
