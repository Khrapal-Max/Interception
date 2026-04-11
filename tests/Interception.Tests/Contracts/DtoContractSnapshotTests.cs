//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.Json;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Reports.Dtos;
using FluentAssertions;

namespace Interception.Tests.Contracts;

/// <summary>
/// Contract tests для ключових міжконтекстних DTO.
/// Фіксують shape JSON (camelCase) як lightweight snapshot.
/// </summary>
public sealed class DtoContractSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void InterceptionListItemDto_ShouldKeepContractShape()
    {
        var dto = new InterceptionListItemDto
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ObservedDate = new DateTime(2026, 04, 11, 12, 00, 00, DateTimeKind.Utc),
            Frequency = "145.500",
            VectorSignal = "V-01",
            Division = "D1",
            ActionName = "Transmit",
            Participants = [new ParticipantBriefDto { Name = "Alpha", IsUnknown = false, Ordinal = 1 }],
            Labels = ["urgent"]
        };

        var root = SerializeToRoot(dto);

        GetPropertyNames(root).Should().Equal([
            "id",
            "observedDate",
            "frequency",
            "vectorSignal",
            "division",
            "actionName",
            "participants",
            "labels"
        ]);
    }

    [Fact]
    public void CandidateGroupDto_ShouldKeepContractShape()
    {
        var dto = new CandidateGroupDto
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Status = CandidateGroupStatusDto.Open,
            ConfidenceScore = 0.82,
            SuggestedName = "Alpha",
            SuggestedRole = "Lead",
            SuggestedDivision = "D1",
            Reasons = new CandidateGroupReasonsDto
            {
                SameFrequency = true,
                SameVector = true,
                SharedPartners = true
            },
            Refs =
            [
                new CandidateGroupRefDto
                {
                    MessageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    ParticipantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    Ordinal = 1,
                    ObservedDate = new DateTime(2026, 04, 11, 12, 00, 00, DateTimeKind.Utc),
                    Frequency = "145.500",
                    VectorSignal = "V-01",
                    Division = "D1"
                }
            ],
            CreatedAt = new DateTime(2026, 04, 11, 12, 01, 00, DateTimeKind.Utc),
            ResolvedParticipantId = null
        };

        var root = SerializeToRoot(dto);

        GetPropertyNames(root).Should().Equal([
            "id",
            "status",
            "confidenceScore",
            "suggestedName",
            "suggestedRole",
            "suggestedDivision",
            "reasons",
            "refs",
            "resolvedBy",
            "resolvedAt",
            "createdAt",
            "resolvedParticipantId"
        ]);
    }

    [Fact]
    public void DayPictureDto_ShouldKeepContractShape()
    {
        var dto = new DayPictureDto(
            Day: new DateTime(2026, 04, 11, 00, 00, 00, DateTimeKind.Utc),
            TotalMessages: 3,
            Groups:
            [
                new DayPictureGroupDto(
                    GroupKey: "145.500|D1|V-01",
                    Division: "D1",
                    MessageCount: 3,
                    Conversations:
                    [
                        new DayPictureConversationDto(
                            ConversationKey: "conv-1",
                            StartedAtUtc: new DateTime(2026, 04, 11, 11, 59, 00, DateTimeKind.Utc),
                            EndedAtUtc: new DateTime(2026, 04, 11, 12, 05, 00, DateTimeKind.Utc),
                            MessageCount: 1,
                            Entries:
                            [
                                new DayPictureEntryDto(
                                    MessageId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                                    ObservedDate: new DateTime(2026, 04, 11, 12, 00, 00, DateTimeKind.Utc),
                                    Frequency: "145.500",
                                    Division: "D1",
                                    VectorSignal: "V-01",
                                    ActionName: "Transmit",
                                    Note: "note",
                                    Participants: ["Alpha"])
                            ])
                    ])
            ]);

        var root = SerializeToRoot(dto);
        GetPropertyNames(root).Should().Equal(["day", "totalMessages", "groups"]);
    }

    private static JsonElement SerializeToRoot<T>(T value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        return doc.RootElement.Clone();
    }

    private static IReadOnlyList<string> GetPropertyNames(JsonElement root)
        => [.. root.EnumerateObject().Select(p => p.Name)];
}
