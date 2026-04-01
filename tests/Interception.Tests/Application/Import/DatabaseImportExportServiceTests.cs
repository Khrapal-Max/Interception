//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Import.Services;

namespace Interception.Tests.Application.Import;

public sealed class DatabaseImportExportServiceTests
{
    [Fact]
    public async Task ExportAsync_InMemoryProvider_ThrowsNotSupportedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = new DatabaseImportExportService(factory);

        await using var output = new MemoryStream();

        var action = async () => await service.ExportAsync(output, ct);

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PostgreSQL*");
    }
}
