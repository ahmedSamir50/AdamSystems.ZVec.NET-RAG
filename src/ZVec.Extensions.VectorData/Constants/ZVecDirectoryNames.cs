namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Directory names excluded when enumerating native collection folders on disk.
/// </summary>
public static class ZVecDirectoryNames
{
    /// <summary>Build output directory name.</summary>
    public const string Bin = "bin";

    /// <summary>Intermediate build directory name.</summary>
    public const string Obj = "obj";

    /// <summary>Application log directory name.</summary>
    public const string Logs = "logs";

    /// <summary>Node package manager directory name.</summary>
    public const string NodeModules = "node_modules";

    /// <summary>Visual Studio metadata directory name.</summary>
    public const string Vs = ".vs";

    /// <summary>JetBrains IDE metadata directory name.</summary>
    public const string Idea = ".idea";

    /// <summary>Git metadata directory name.</summary>
    public const string Git = ".git";

    /// <summary>
    /// All infrastructure directory names excluded from collection enumeration.
    /// </summary>
    public static readonly string[] CollectionEnumerationExclusions =
    [
        Bin,
        Obj,
        Logs,
        NodeModules,
        Vs,
        Idea,
        Git
    ];
}
