namespace BackupRestore.Api.Services;

/// <summary>
/// Configures how user-supplied host paths (e.g. Windows paths typed in the UI)
/// map to the path that is actually visible inside the container.
/// </summary>
public sealed class HostPathOptions
{
    /// <summary>The host root that is bind-mounted into the container, e.g. "C:\Users\me".</summary>
    public string? WindowsRoot { get; set; }

    /// <summary>Where that root is mounted inside the container, e.g. "/host".</summary>
    public string? ContainerRoot { get; set; }
}

/// <summary>
/// Translates a user-supplied path (any real path under <see cref="HostPathOptions.WindowsRoot"/>)
/// into the equivalent path inside the container. When no mapping is configured
/// (for example when the API runs natively on the host), the input is returned unchanged.
/// </summary>
public sealed class HostPathTranslator
{
    private readonly string? _winRoot;
    private readonly string? _containerRoot;

    public HostPathTranslator(HostPathOptions options)
    {
        _winRoot = Normalize(options.WindowsRoot);
        _containerRoot = Normalize(options.ContainerRoot);
    }

    public bool IsEnabled => _winRoot is not null && _containerRoot is not null;

    public string ToContainerPath(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || _winRoot is null || _containerRoot is null)
        {
            return input;
        }

        var normalized = input.Replace('\\', '/').TrimEnd('/');

        // Already a container-side path — leave it alone.
        if (normalized.StartsWith(_containerRoot, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/data", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.Equals(_winRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _containerRoot;
        }

        if (normalized.StartsWith(_winRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return _containerRoot + normalized[_winRoot.Length..];
        }

        // Outside the mounted root: return normalized input so validation reports it clearly.
        return normalized;
    }

    private static string? Normalize(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/').TrimEnd('/');
}
