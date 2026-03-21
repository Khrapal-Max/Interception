//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class PatternMatchReasonsTests
{
    [Fact]
    public void MatchCount_AllTrue_ShouldReturnFive()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency    = true,
            SameVector       = true,
            SamePointSignal  = true,
            SameDivision     = true,
            CloseInTime      = true
        };

        reasons.MatchCount.Should().Be(5);
    }

    [Fact]
    public void MatchCount_AllFalse_ShouldReturnZero()
    {
        var reasons = new PatternMatchReasons();

        reasons.MatchCount.Should().Be(0);
    }

    [Theory]
    [InlineData(true,  false, false, false, false, 1)]
    [InlineData(true,  true,  false, false, false, 2)]
    [InlineData(true,  true,  true,  false, false, 3)]
    [InlineData(true,  true,  true,  true,  false, 4)]
    public void MatchCount_PartialMatch_ShouldReturnCorrectCount(
        bool freq, bool vec, bool point, bool div, bool time, int expected)
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency    = freq,
            SameVector       = vec,
            SamePointSignal  = point,
            SameDivision     = div,
            CloseInTime      = time
        };

        reasons.MatchCount.Should().Be(expected);
    }
}

public sealed class PatternRecognitionOptionsTests
{
    [Fact]
    public void WeightSumIsValid_DefaultValues_ShouldReturnTrue()
    {
        var options = new PatternRecognitionOptions();

        options.WeightSumIsValid().Should().BeTrue();
    }

    [Fact]
    public void WeightSumIsValid_CustomValidWeights_ShouldReturnTrue()
    {
        var options = new PatternRecognitionOptions
        {
            FrequencyWeight   = 0.40,
            VectorWeight      = 0.30,
            PointSignalWeight = 0.15,
            DivisionWeight    = 0.10,
            TimeWeight        = 0.05
        };

        options.WeightSumIsValid().Should().BeTrue();
    }

    [Fact]
    public void WeightSumIsValid_InvalidWeights_ShouldReturnFalse()
    {
        var options = new PatternRecognitionOptions
        {
            FrequencyWeight   = 0.50,
            VectorWeight      = 0.50,
            PointSignalWeight = 0.50,
            DivisionWeight    = 0.50,
            TimeWeight        = 0.50
        };

        options.WeightSumIsValid().Should().BeFalse();
    }
}
