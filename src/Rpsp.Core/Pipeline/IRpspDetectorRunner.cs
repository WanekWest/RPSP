using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Pipeline;

public interface IRpspDetectorRunner
{
    DetectorPipelineRunResult Run(
        IReadOnlyList<RadioObservation> observations,
        BaselineProfile baseline);
}
