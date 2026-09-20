using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Snapshot of all competitors (number + total laps) from the current XML read.
    /// Used to count how many teams have crossed the line after the race expires.
    /// </summary>
    public List<(string Id, int Laps)> AllCompetitors { get; } = new();

    public TimeSpan Remaining => TotalDuration - Elapsed;
}

