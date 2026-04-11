//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain.Entities;

namespace Interception.Tests.Domain;

public sealed class PatternMatchReasonsTests
{
    // -------------------------------------------------------------------------
    // MatchCount
    // -------------------------------------------------------------------------

    [Fact]
    public void MatchCount_AllTrue_ShouldReturnSeven()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency = true,
            SameVector = true,
            SamePointSignal = true,
            SameDivision = true,
            CloseInTime = true,
            SharedPartners = true,
            SharedLabels = true
        };

        reasons.MatchCount.Should().Be(7);
    }

    [Fact]
    public void MatchCount_AllFalse_ShouldReturnZero()
    {
        var reasons = new PatternMatchReasons();

        reasons.MatchCount.Should().Be(0);
    }

    [Fact]
    public void MatchCount_OnlyFrequency_ReturnsOne()
    {
        var reasons = new PatternMatchReasons { SameFrequency = true };
        reasons.MatchCount.Should().Be(1);
    }

    [Fact]
    public void MatchCount_FrequencyAndVector_ReturnsTwo()
    {
        var reasons = new PatternMatchReasons { SameFrequency = true, SameVector = true };
        reasons.MatchCount.Should().Be(2);
    }

    [Fact]
    public void MatchCount_FrequencyVectorPartners_ReturnsThree()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency = true,
            SameVector = true,
            SharedPartners = true
        };
        reasons.MatchCount.Should().Be(3);
    }

    [Fact]
    public void MatchCount_FrequencyVectorTimePartners_ReturnsFour()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency = true,
            SameVector = true,
            CloseInTime = true,
            SharedPartners = true
        };
        reasons.MatchCount.Should().Be(4);
    }

    [Fact]
    public void MatchCount_FrequencyVectorTimePartnersLabels_ReturnsFive()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency = true,
            SameVector = true,
            CloseInTime = true,
            SharedPartners = true,
            SharedLabels = true
        };
        reasons.MatchCount.Should().Be(5);
    }

    [Fact]
    public void MatchCount_AllSignalFields_ReturnsFour()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency = true,
            SameVector = true,
            SamePointSignal = true,
            SameDivision = true
        };
        reasons.MatchCount.Should().Be(4);
    }

    [Fact]
    public void MatchCount_AllSignalFieldsPlusTime_ReturnsFive()
    {
        var reasons = new PatternMatchReasons
        {
            SameFrequency = true,
            SameVector = true,
            SamePointSignal = true,
            SameDivision = true,
            CloseInTime = true
        };
        reasons.MatchCount.Should().Be(5);
    }

    [Fact]
    public void MatchCount_SharedPartners_TrueOnly_MatchCountIsOne()
    {
        var reasons = new PatternMatchReasons { SharedPartners = true };
        reasons.MatchCount.Should().Be(1);
    }

    [Fact]
    public void MatchCount_SharedLabels_TrueOnly_MatchCountIsOne()
    {
        var reasons = new PatternMatchReasons { SharedLabels = true };
        reasons.MatchCount.Should().Be(1);
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
            FrequencyWeight = 0.30,
            VectorWeight = 0.25,
            SharedPartnersWeight = 0.20,
            PointSignalWeight = 0.10,
            DivisionWeight = 0.08,
            TimeWeight = 0.05,
            SharedLabelsWeight = 0.02
        };

        options.WeightSumIsValid().Should().BeTrue();
    }

    [Fact]
    public void WeightSumIsValid_InvalidWeights_ShouldReturnFalse()
    {
        var options = new PatternRecognitionOptions
        {
            FrequencyWeight = 0.50,
            VectorWeight = 0.50,
            SharedPartnersWeight = 0.50,
            PointSignalWeight = 0.50,
            DivisionWeight = 0.50,
            TimeWeight = 0.50,
            SharedLabelsWeight = 0.50
        };

        options.WeightSumIsValid().Should().BeFalse();
    }

    [Fact]
    public void WeightSumIsValid_WithinTolerance_ShouldReturnTrue()
    {
        var options = new PatternRecognitionOptions
        {
            FrequencyWeight = 0.301,
            VectorWeight = 0.250,
            SharedPartnersWeight = 0.200,
            PointSignalWeight = 0.100,
            DivisionWeight = 0.080,
            TimeWeight = 0.050,
            SharedLabelsWeight = 0.020
        };

        options.WeightSumIsValid().Should().BeTrue();
    }

    [Fact]
    public void MinSharedPartners_DefaultValue_IsTwo()
    {
        new PatternRecognitionOptions().MinSharedPartners.Should().Be(2);
    }

    [Fact]
    public void TimeWindowMinutes_DefaultValue_IsThirty()
    {
        new PatternRecognitionOptions().TimeWindowMinutes.Should().Be(30);
    }
}
