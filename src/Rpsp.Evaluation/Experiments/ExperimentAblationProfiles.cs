using Rpsp.Core.Baselines;
using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Core.Pipeline;
using Rpsp.Core.Scoring;

namespace Rpsp.Evaluation.Experiments;

public static class ExperimentAblationProfiles
{
    public const string Full = "full";
    public const string StationaryOnly = "stationary-only";
    public const string RatOnly = "rat-only";
    public const string UnknownCellOnly = "unknown-cell-only";
    public const string NeighborGraphOnly = "neighbor-graph-only";
    public const string NoMotionSuppression = "no-motion-suppression";
    public const string ReducedEvidenceGates = "reduced-evidence-gates";
    public const string NoCompositeBoost = "no-composite-boost";

    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        Full,
        StationaryOnly,
        RatOnly,
        UnknownCellOnly,
        NeighborGraphOnly,
        NoMotionSuppression,
        ReducedEvidenceGates,
        NoCompositeBoost
    };

    public static void Validate(IReadOnlyList<string> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (profiles.Count == 0)
        {
            throw new InvalidOperationException("At least one ablation profile is required.");
        }

        string[] invalid = profiles
            .Where(profile => !Supported.Contains(profile))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (invalid.Length != 0)
        {
            throw new InvalidOperationException($"Unsupported ablation profiles: {string.Join(", ", invalid)}.");
        }

        if (profiles.Distinct(StringComparer.Ordinal).Count() != profiles.Count)
        {
            throw new InvalidOperationException("Ablation profiles must be unique.");
        }
    }

    public static RpspDetectionPipeline CreatePipeline(string profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        RpspDetectionPipelineOptions options = profile switch
        {
            NoMotionSuppression => CreateNoMotionSuppressionOptions(),
            ReducedEvidenceGates => CreateReducedEvidenceGateOptions(),
            NoCompositeBoost => new RpspDetectionPipelineOptions
            {
                CompositeRiskScorerOptions = new CompositeRiskScorerOptions
                {
                    AdditionalSuspiciousBoost = 0D,
                    AdditionalHighRiskBoost = 0D,
                    MaximumCorrelationBoost = 0D
                }
            },
            _ when Supported.Contains(profile) => new RpspDetectionPipelineOptions(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown ablation profile.")
        };

        string? detector = profile switch
        {
            StationaryOnly => "StationaryHandoverAnomalyDetector",
            RatOnly => "RatDowngradeRiskDetector",
            UnknownCellOnly => "UnknownStrongCellDetector",
            NeighborGraphOnly => "NeighborGraphAnomalyDetector",
            _ => null
        };

        if (detector is null)
        {
            return new RpspDetectionPipeline(options);
        }

        var runner = new FilteringDetectorRunner(new StockCoreDetectorRunner(options), detector);
        return new RpspDetectionPipeline(options, runner);
    }

    private static RpspDetectionPipelineOptions CreateNoMotionSuppressionOptions()
    {
        var emptyMotionStates = new HashSet<DeviceMotionState>();

        return new RpspDetectionPipelineOptions
        {
            StationaryHandoverOptions = new StationaryHandoverDetectorOptions
            {
                BenignMotionStates = emptyMotionStates
            },
            RatDowngradeOptions = new RatDowngradeDetectorOptions
            {
                BenignMotionStates = emptyMotionStates
            },
            UnknownStrongCellOptions = new UnknownStrongCellDetectorOptions
            {
                BenignMotionStates = emptyMotionStates
            },
            NeighborGraphOptions = new NeighborGraphDetectorOptions
            {
                BenignMotionStates = emptyMotionStates
            }
        };
    }

    private static RpspDetectionPipelineOptions CreateReducedEvidenceGateOptions()
    {
        return new RpspDetectionPipelineOptions
        {
            StationaryHandoverOptions = new StationaryHandoverDetectorOptions
            {
                MinimumObservationCount = 1,
                MinimumWindowDuration = TimeSpan.FromMilliseconds(1D)
            },
            RatDowngradeOptions = new RatDowngradeDetectorOptions
            {
                MinimumObservationCount = 1,
                MinimumWindowDuration = TimeSpan.FromMilliseconds(1D),
                MinimumModernRatHistoryCount = 1
            },
            UnknownStrongCellOptions = new UnknownStrongCellDetectorOptions
            {
                MinimumObservationCount = 1,
                MinimumWindowDuration = TimeSpan.FromMilliseconds(1D),
                MinimumTrustedBaselineCellCount = 1,
                MinimumUnknownStrongAppearances = 1
            },
            NeighborGraphOptions = new NeighborGraphDetectorOptions
            {
                MinimumObservationCount = 1,
                MinimumWindowDuration = TimeSpan.FromMilliseconds(1D),
                MinimumBaselineNeighborCellCount = 1,
                MinimumObservedNeighborCellCount = 1,
                MinimumLowSimilarityObservations = 1
            }
        };
    }

    private sealed class FilteringDetectorRunner(
        IRpspDetectorRunner inner,
        string detectorName) : IRpspDetectorRunner
    {
        public DetectorPipelineRunResult Run(
            IReadOnlyList<RadioObservation> observations,
            BaselineProfile baseline)
        {
            DetectorPipelineRunResult result = inner.Run(observations, baseline);

            return result with
            {
                Contributions = result.Contributions
                    .Where(contribution => string.Equals(contribution.DetectorName, detectorName, StringComparison.Ordinal))
                    .ToArray(),
                DetectorReports = result.DetectorReports
                    .Where(report => string.Equals(report.DetectorName, detectorName, StringComparison.Ordinal))
                    .ToArray()
            };
        }
    }
}
