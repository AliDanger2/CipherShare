namespace CipherShare.Models;

/// <summary>
/// Controls whether an incoming transfer needs to be accepted by hand in the
/// IncomingTransferDialog, or whether it can start automatically.
/// </summary>
public enum SecurityLevel
{
    /// <summary>Every incoming transfer, from any device, must be accepted manually.</summary>
    RequireConfirmationForAll,

    /// <summary>Trusted devices skip the confirmation dialog; everyone else still needs approval.</summary>
    SkipConfirmationForTrusted,

    /// <summary>No confirmation is ever required. Use with care on untrusted networks.</summary>
    NoConfirmationRequired
}
