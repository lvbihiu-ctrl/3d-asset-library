namespace AssetLibrary.Desktop;

internal sealed record HostOptions(
    bool SmokeTest,
    bool UiInteractionTest,
    string? LibraryTestRoot,
    string? CaptureDirectory,
    string? UserDataDirectory,
    string? DataDirectory = null,
    bool CoverFeatureTest = false,
    string? LegacyDataDirectory = null,
    bool MultiFolderTest = false,
    bool LargeLibraryTest = false)
{
    public bool IsAutomated => SmokeTest || UiInteractionTest || LibraryTestRoot is not null || CaptureDirectory is not null || CoverFeatureTest || MultiFolderTest || LargeLibraryTest;

    public static HostOptions Parse(IReadOnlyList<string> arguments)
    {
        var smokeTest = false;
        var uiInteractionTest = false;
        string? libraryTestRoot = null;
        string? captureDirectory = null;
        string? userDataDirectory = null;
        string? dataDirectory = null;
        var coverFeatureTest = false;
        var multiFolderTest = false;
        var largeLibraryTest = false;
        string? legacyDataDirectory = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--large-library-test":
                    largeLibraryTest = true;
                    break;
                case "--multi-folder-test":
                    multiFolderTest = true;
                    break;
                case "--data-dir":
                    dataDirectory = ReadValue(arguments, ref index, "--data-dir");
                    break;
                case "--cover-feature-test":
                    coverFeatureTest = true;
                    break;
                case "--legacy-data-dir":
                    legacyDataDirectory = ReadValue(arguments, ref index, "--legacy-data-dir");
                    break;
                case "--smoke-test":
                    smokeTest = true;
                    break;
                case "--ui-interaction-test":
                    uiInteractionTest = true;
                    break;
                case "--library-test-root":
                    libraryTestRoot = ReadValue(arguments, ref index, "--library-test-root");
                    break;
                case "--capture-directory":
                    captureDirectory = ReadValue(arguments, ref index, "--capture-directory");
                    break;
                case "--user-data-dir":
                    userDataDirectory = ReadValue(arguments, ref index, "--user-data-dir");
                    break;
                default:
                    throw new ArgumentException($"未知参数：{arguments[index]}");
            }
        }

        return new HostOptions(
            smokeTest,
            uiInteractionTest,
            libraryTestRoot is null ? null : Path.GetFullPath(libraryTestRoot),
            captureDirectory is null ? null : Path.GetFullPath(captureDirectory),
            userDataDirectory is null ? null : Path.GetFullPath(userDataDirectory),
            dataDirectory is null ? null : Path.GetFullPath(dataDirectory),
            coverFeatureTest,
            legacyDataDirectory is null ? null : Path.GetFullPath(legacyDataDirectory), multiFolderTest, largeLibraryTest);
    }

    private static string ReadValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (++index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index]))
        {
            throw new ArgumentException($"{option} 缺少路径参数。");
        }

        return arguments[index];
    }
}
