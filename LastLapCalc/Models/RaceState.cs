using System;

namespace LastLapCalc.Models;

/// <summary>
/// Represents the current state of the race relevant to prediction.
/// </summary>
public sealed class RaceState
{
    /// <summary>
    /// Elapsed race time since start.
    /// </summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>
    /// Total configured race duration.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// The leader (1st place) competitor state.
    /// </summary>
    public CompetitorState Leader { get; set; } = new();

    public TimeSpan Remaining => TotalDuration - Elapsed;
}

