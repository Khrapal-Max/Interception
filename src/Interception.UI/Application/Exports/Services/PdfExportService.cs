//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Exports.Dtos;
using Interception.UI.Application.Exports.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Interception.UI.Application.Exports.Services;

/// <summary>
/// PDF-експорт для аналітичних сторінок.
/// Наразі підтримує людський звіт по ієрархії груп.
/// </summary>
public sealed class PdfExportService(IGroupHierarchyService groupHierarchyService) : IPdfExportService
{
    private const string PdfContentType = "application/pdf";
    private readonly IGroupHierarchyService _groupHierarchyService = groupHierarchyService;

    /// <inheritdoc />
    public async Task<ExportFileDto> ExportAsync(ExportRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        QuestPDF.Settings.License = LicenseType.Community;

        return request.Kind switch
        {
            ExportKind.GroupHierarchy => await ExportGroupHierarchyAsync(request, ct),
            _ => throw new NotSupportedException($"PDF-експорт для '{request.Kind}' ще не підтримується.")
        };
    }

    private async Task<ExportFileDto> ExportGroupHierarchyAsync(ExportRequestDto request, CancellationToken ct)
    {
        var hierarchy = await _groupHierarchyService.BuildAsync(request.DateFrom, request.DateTo, ct);
        var title = "Ієрархія груп";
        var fileName = $"group_hierarchy_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Element(c => ComposeHeader(c, title, request));
                page.Content().Element(c => ComposeContent(c, hierarchy));
                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Сторінка ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        var bytes = document.GeneratePdf();
        return new ExportFileDto(fileName, PdfContentType, bytes);
    }

    private static void ComposeHeader(IContainer container, string title, ExportRequestDto request)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(20).SemiBold();
            column.Item().PaddingTop(4).Text(BuildPeriodText(request)).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent(IContainer container, GroupHierarchyDto hierarchy)
    {
        container.Column(column =>
        {
            column.Spacing(14);

            column.Item().Element(c => ComposeSummary(c, hierarchy));

            if (hierarchy.Clusters.Count == 0)
            {
                column.Item().PaddingTop(12).Text("За вибраний період ієрархії груп не знайдено.")
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            foreach (var cluster in hierarchy.Clusters)
                column.Item().Element(c => ComposeCluster(c, cluster));
        });
    }

    private static void ComposeSummary(IContainer container, GroupHierarchyDto hierarchy)
    {
        var totalClusters = hierarchy.Clusters.Count;
        var totalGroups = hierarchy.Clusters.Sum(x => x.TotalGroups);
        var totalTransitions = hierarchy.Clusters.Sum(x => x.TransitionCount);
        var totalDirectBridges = hierarchy.Clusters.Sum(x => x.DirectBridgeCount);

        container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Кластерів").FontColor(Colors.Grey.Darken1);
                c.Item().Text(totalClusters.ToString()).FontSize(18).SemiBold();
            });
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Груп усього").FontColor(Colors.Grey.Darken1);
                c.Item().Text(totalGroups.ToString()).FontSize(18).SemiBold();
            });
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Переходів").FontColor(Colors.Grey.Darken1);
                c.Item().Text(totalTransitions.ToString()).FontSize(18).SemiBold();
            });
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Прямих мостів").FontColor(Colors.Grey.Darken1);
                c.Item().Text(totalDirectBridges.ToString()).FontSize(18).SemiBold();
            });
        });
    }

    private static void ComposeCluster(IContainer container, GroupHierarchyClusterDto cluster)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Spacing(8);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Опорна група: {cluster.RootCenterName}").FontSize(14).SemiBold();
                    c.Item().Text(BuildClusterSummary(cluster)).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(240).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Груп: {cluster.TotalGroups}");
                    c.Item().AlignRight().Text($"Переходів: {cluster.TransitionCount}");
                    c.Item().AlignRight().Text($"Прямих мостів: {cluster.DirectBridgeCount}");
                });
            });

            if (cluster.TopActions.Count > 0)
                column.Item().Text($"Дії: {string.Join(", ", cluster.TopActions)}").FontColor(Colors.Blue.Darken2);

            column.Item().Element(c => ComposeNodesTable(c, cluster.Nodes));

            if (cluster.Transitions.Count > 0)
            {
                column.Item().PaddingTop(4).Text("Переходи між рівнями").SemiBold();
                column.Item().Element(c => ComposeTransitionsTable(c, cluster.Transitions));
            }
        });
    }

    private static void ComposeNodesTable(IContainer container, IReadOnlyList<GroupHierarchyNodeDto> nodes)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(42);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.4f);
                columns.ConstantColumn(56);
                columns.ConstantColumn(60);
            });

            table.Header(header =>
            {
                HeaderCell(header, "Рівень");
                HeaderCell(header, "Центр / група");
                HeaderCell(header, "Підрозділ");
                HeaderCell(header, "Частоти");
                HeaderCell(header, "Перехідний учасник");
                HeaderCell(header, "Учасн.");
                HeaderCell(header, "Діти");
            });

            foreach (var node in nodes.OrderBy(x => x.Level).ThenBy(x => x.CenterName, StringComparer.OrdinalIgnoreCase))
            {
                BodyCell(table, node.Level.ToString());
                BodyCell(table, node.IsRoot ? $"{node.CenterName} (root)" : node.CenterName);
                BodyCell(table, string.IsNullOrWhiteSpace(node.Division) ? "НВ підрозділ" : node.Division!);
                BodyCell(table, node.Frequencies.Count == 0 ? "—" : string.Join(", ", node.Frequencies));
                BodyCell(table, string.IsNullOrWhiteSpace(node.TransitionMemberName)
                    ? "—"
                    : string.IsNullOrWhiteSpace(node.TransitionMemberRole)
                        ? node.TransitionMemberName!
                        : $"{node.TransitionMemberName} / {node.TransitionMemberRole}");
                BodyCell(table, node.MemberCount.ToString());
                BodyCell(table, node.ChildCount.ToString());
            }
        });
    }

    private static void ComposeTransitionsTable(IContainer container, IReadOnlyList<GroupHierarchyTransitionDto> transitions)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(70);
            });

            table.Header(header =>
            {
                HeaderCell(header, "Батьківська група");
                HeaderCell(header, "Дочірня група");
                HeaderCell(header, "Через учасника");
                HeaderCell(header, "Прямий міст");
            });

            foreach (var transition in transitions.OrderByDescending(x => x.Score))
            {
                BodyCell(table, transition.ParentCenterName);
                BodyCell(table, transition.ChildCenterName);
                BodyCell(table, string.IsNullOrWhiteSpace(transition.TransitionMemberRole)
                    ? transition.TransitionMemberName
                    : $"{transition.TransitionMemberName} / {transition.TransitionMemberRole}");
                BodyCell(table, transition.HasDirectBridge ? "Так" : "Ні");
            }
        });
    }

    private static void HeaderCell(TableCellDescriptor descriptor, string text)
        => descriptor.Cell().Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
            .Text(text).SemiBold();

    private static void BodyCell(TableDescriptor descriptor, string text)
        => descriptor.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(5).Text(text);

    private static string BuildPeriodText(ExportRequestDto request)
    {
        if (!request.DateFrom.HasValue && !request.DateTo.HasValue)
            return "Період: весь доступний діапазон";

        var from = request.DateFrom?.ToString("dd.MM.yyyy") ?? "…";
        var to = request.DateTo?.ToString("dd.MM.yyyy") ?? "…";
        return $"Період: {from} — {to}";
    }

    private static string BuildClusterSummary(GroupHierarchyClusterDto cluster)
    {
        var division = string.IsNullOrWhiteSpace(cluster.Division) ? "НВ підрозділ" : cluster.Division;
        var frequencies = cluster.Frequencies.Count == 0 ? "частоти не визначені" : string.Join(", ", cluster.Frequencies);
        return $"Підрозділ: {division}. Частоти: {frequencies}. Унікальних осіб: {cluster.TotalUniqueMembers}.";
    }
}
