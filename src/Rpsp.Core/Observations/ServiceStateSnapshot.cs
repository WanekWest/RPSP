namespace Rpsp.Core.Observations;

public sealed record ServiceStateSnapshot(
    bool? IsInService = null,
    bool? IsRoaming = null,
    RadioAccessTechnology? VoiceRadioAccessTechnology = null,
    RadioAccessTechnology? DataRadioAccessTechnology = null,
    bool? IsEmergencyOnly = null);
