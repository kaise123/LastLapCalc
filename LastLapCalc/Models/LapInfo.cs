using System;

namespace LastLapCalc.Models;

/// <summary>
/// Represents a single completed lap for the leader.
/// </summary>
public sealed class LapInfo
{
    public int LapNumber { get; init; }

    /// <summary>
    /// Duration of this lap.
    /// </summary>
    public TimeSpan LapTime { get; init; }

    /// <summary>
    /// UTC time when this lap was completed (if available).
    /// </summary>
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// Time of day when this lap was completed (if available).
    /// </summary>
    public DateTime? CompletedAtTimeOfDay { get; init; }

    /// <summary>
    /// Elapsed race time when this lap was completed.
    /// </summary>
    public TimeSpan? ElapsedAtCompletion { get; init; }
}

