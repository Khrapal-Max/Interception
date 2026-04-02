//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Extensions;

/// <summary>
/// Допоміжна логіка для app-relative шляхів у portable-режимі.
/// </summary>
public static class RuntimePathExtensions
{
    /// <summary>
    /// Перетворює app-relative шлях (наприклад <c>./data/file.db</c>) на абсолютний шлях
    /// відносно каталогу запуску застосунку.
    /// </summary>
    public static string ResolveAppRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        if (Path.IsPathRooted(path))
            return path;

        var normalized = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart('.', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalized));
    }
}
