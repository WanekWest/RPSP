using System.Text;
using Xunit;

namespace Rpsp.Evaluation.Tests.Support;

internal static class SnapshotAssert
{
    public static string SnapshotDirectory { get; } = FindSnapshotDirectory();

    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n');
    }

    public static void MatchesSnapshot(string fileName, string actual)
    {
        string snapshotPath = Path.Combine(SnapshotDirectory, fileName);

        Assert.True(File.Exists(snapshotPath), $"Snapshot file is missing: {snapshotPath}");

        string expected = Normalize(File.ReadAllText(snapshotPath, Encoding.UTF8));
        string normalizedActual = Normalize(actual);

        if (!string.Equals(expected, normalizedActual, StringComparison.Ordinal))
        {
            Assert.Fail(CreateMismatchMessage(fileName, expected, normalizedActual));
        }
    }

    private static string FindSnapshotDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string projectFile = Path.Combine(directory.FullName, "Rpsp.Evaluation.Tests.csproj");

            if (File.Exists(projectFile))
            {
                return Path.Combine(directory.FullName, "Snapshots");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Rpsp.Evaluation.Tests project directory.");
    }

    private static string CreateMismatchMessage(string fileName, string expected, string actual)
    {
        int lineNumber = FindFirstDifferentLine(expected, actual);

        return string.Create(
            null,
            $"Snapshot mismatch for {fileName} at line {lineNumber}. Actual snapshot:\n--- actual start ---\n{actual}\n--- actual end ---");
    }

    private static int FindFirstDifferentLine(string expected, string actual)
    {
        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        int maxLineCount = Math.Max(expectedLines.Length, actualLines.Length);

        for (int index = 0; index < maxLineCount; index++)
        {
            string expectedLine = index < expectedLines.Length ? expectedLines[index] : string.Empty;
            string actualLine = index < actualLines.Length ? actualLines[index] : string.Empty;

            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        return 0;
    }
}
