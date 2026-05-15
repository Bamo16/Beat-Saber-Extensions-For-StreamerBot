namespace BuildBeatSaberExtensions.Utility;

public static class PathUtility
{
    public static string ResolvePath(string rootPath, string path)
    {
        rootPath = rootPath.ValidatePathArg(nameof(rootPath));
        path = path.ValidatePathArg(nameof(path));

        var absoluteRootPath = Path.GetFullPath(rootPath);

        var resolvedPath = path switch
        {
            _ when path.StartsWith(Path.DirectorySeparatorChar)
                    || path.StartsWith(Path.AltDirectorySeparatorChar) => Path.Combine(
                absoluteRootPath,
                path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            ),
            _ when Path.IsPathRooted(path) => path,
            _ => Path.Combine(absoluteRootPath, path),
        };

        try
        {
            var finalPath = Path.GetFullPath(resolvedPath);

            if (!finalPath.StartsWith(absoluteRootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    "Path resolution attempted to escape the root directory."
                );
            }

            return finalPath;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            throw new ArgumentException(
                $"Failed to resolve path '{path}' relative to '{rootPath}': {ex.Message}",
                nameof(path),
                ex
            );
        }
    }

    public static string GetRelativePath(string rootPath, string absolutePath)
    {
        rootPath = rootPath.ValidatePathArg(nameof(rootPath));
        absolutePath = absolutePath.ValidatePathArg(nameof(absolutePath), isAbsolute: true);

        try
        {
            return Path.GetRelativePath(rootPath, absolutePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to compute relative path from '{rootPath}' to '{absolutePath}': {ex.Message}",
                nameof(absolutePath),
                ex
            );
        }
    }

    private static string ValidatePathArg(this string path, string argName, bool isAbsolute = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"The {argName} argument cannot be null, empty, or whitespace.",
                argName
            );
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException(
                $"The {argName} argument contains invalid characters.",
                argName
            );
        }

        if (isAbsolute && !Path.IsPathRooted(path))
        {
            throw new ArgumentException(
                $"The {argName} argument must be an absolute path.",
                argName
            );
        }

        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
