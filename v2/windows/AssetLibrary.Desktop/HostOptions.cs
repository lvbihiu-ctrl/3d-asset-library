namespace AssetLibrary.Desktop;

internal sealed record HostOptions(
    bool SmokeTest,
    string? CaptureDirectory,
    string? UserDataDirectory)
{
    public bool IsAutomated => SmokeTest || CaptureDirectory is not null;

    public static HostOptions Parse(IReadOnlyList<string> arguments)
    {
        var smokeTest = false;
        string? captureDirectory = null;
        string? userDataDirectory = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--smoke-test":
                    smokeTest = true;
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
