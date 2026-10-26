using System.Reflection;
using Rpsp.Core.Observations;
using Xunit;

namespace Rpsp.Core.Tests;

public sealed class PublicDomainModelSafetyTests
{
    [Fact]
    public void PublicDomainModelsDoNotExposeSubscriberOrDeviceIdentifierNames()
    {
        string[] forbiddenNames =
        [
            string.Concat("IM", "SI"),
            string.Concat("IM", "EI"),
            string.Concat("MS", "IS", "DN"),
            string.Concat("SU", "PI"),
            string.Concat("SU", "CI")
        ];

        IEnumerable<string> publicNames = typeof(RadioObservation).Assembly
            .GetExportedTypes()
            .SelectMany(static type => new[] { type.Name }.Concat(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Select(static property => property.Name)));

        Assert.DoesNotContain(
            publicNames,
            name => forbiddenNames.Any(forbidden =>
                name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }
}
