using Rpsp.Evaluation.Tests.Support;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class GoldenReportSnapshotTests
{
    public static IEnumerable<object[]> SnapshotCases()
    {
        return ReportSnapshotFactory.Cases.Select(static snapshotCase => new object[] { snapshotCase.FileName });
    }

    [Fact]
    public void AllGoldenSnapshotFilesExist()
    {
        foreach (ReportSnapshotCase snapshotCase in ReportSnapshotFactory.Cases)
        {
            string snapshotPath = Path.Combine(SnapshotAssert.SnapshotDirectory, snapshotCase.FileName);

            Assert.True(File.Exists(snapshotPath), $"Snapshot file is missing: {snapshotPath}");
        }
    }

    [Theory]
    [MemberData(nameof(SnapshotCases))]
    public async Task SnapshotsMatchGoldenFiles(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ReportSnapshotCase snapshotCase = ReportSnapshotFactory.Get(fileName);
        string actual = await snapshotCase.GenerateAsync();

        SnapshotAssert.MatchesSnapshot(snapshotCase.FileName, actual);
    }

    [Theory]
    [MemberData(nameof(SnapshotCases))]
    public async Task SnapshotOutputIsDeterministic(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ReportSnapshotCase snapshotCase = ReportSnapshotFactory.Get(fileName);
        string first = await snapshotCase.GenerateAsync();
        string second = await snapshotCase.GenerateAsync();

        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(SnapshotCases))]
    public async Task SnapshotOutputDoesNotContainForbiddenPhrases(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ReportSnapshotCase snapshotCase = ReportSnapshotFactory.Get(fileName);
        string actual = await snapshotCase.GenerateAsync();
        string[] forbiddenPhrases =
        [
            string.Concat("attack", " confirmed"),
            string.Concat("IM", "SI", "-catcher detected"),
            string.Concat("lawful", " intercept", " detection"),
            string.Concat("definitive", " IM", "SI", "-catcher identification")
        ];

        foreach (string phrase in forbiddenPhrases)
        {
            Assert.DoesNotContain(phrase, actual, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(SnapshotCases))]
    public async Task SnapshotOutputDoesNotContainSensitiveIdentifierTerms(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ReportSnapshotCase snapshotCase = ReportSnapshotFactory.Get(fileName);
        string actual = await snapshotCase.GenerateAsync();
        string[] restrictedTerms =
        [
            string.Concat("IM", "SI"),
            string.Concat("IM", "EI"),
            string.Concat("MS", "IS", "DN"),
            string.Concat("SU", "PI"),
            string.Concat("SU", "CI")
        ];

        foreach (string term in restrictedTerms)
        {
            Assert.DoesNotContain(term, actual, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(SnapshotCases))]
    public async Task SnapshotOutputDoesNotContainRawSyntheticCellIdentifiers(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ReportSnapshotCase snapshotCase = ReportSnapshotFactory.Get(fileName);
        string actual = await snapshotCase.GenerateAsync();

        Assert.DoesNotContain("synthetic-cell", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-rat-cell", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-known", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-unknown", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-neighbor", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-serving", actual, StringComparison.Ordinal);
    }
}
