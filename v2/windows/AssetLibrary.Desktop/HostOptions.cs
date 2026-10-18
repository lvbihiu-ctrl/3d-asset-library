namespace AssetLibrary.Desktop;

internal sealed record HostOptions(
    bool SmokeTest,
    bool UiInteractionTest,
    string? LibraryTestRoot,
    string? CaptureDirectory,
    string? UserDataDirectory)
{
    public bool IsAutomated => SmokeTest || UiInteractionTest || LibraryTestRoot is not null || CaptureDirectory is not null;

    public static HostOptions Parse(IReadOnlyList<string> arguments)
    {
        var smokeTest = false;
        var uiInteractionTest = false;
        string? libraryTestRoot = null;
        string? captureDirectory = null;
        string? userDataDirectory = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
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
            userDataDirectory is null ? null : Path.GetFullPath(userDataDirectory));
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
