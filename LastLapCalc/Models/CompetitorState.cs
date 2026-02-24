using System;
using System.Collections.Generic;
using System.Linq;

namespace LastLapCalc.Models;

/// <summary>
/// Represents the state of a competitor (initially just the race leader).
/// </summary>
public sealed class CompetitorState
{
    /// <summary>
    /// Identifier for the competitor (e.g. bib number, name, or \"Leader\").
    /// </summary>
    public string Id { get; set; } = "Leader";

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
        ? Id
        : $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Sequence of completed laps in order.
    /// </summary>
    public List<LapInfo> Laps { get; } = new List<LapInfo>();

    /// <summary>
    /// Total number of laps completed (from XML).
    /// This may be greater than Laps.Count since we only track the last 10 laps.
    /// </summary>
    public int TotalLapsCompleted { get; set; }

    public int TotalLaps => Laps.Count;

    public LapInfo? LastLap => Laps.LastOrDefault();

    /// <summary>
    /// Computes the average lap time over the last N laps, if enough data exists.
    /// Falls back to using however many laps are available if fewer than windowSize.
    /// Returns null if there are no laps.
    /// </summary>
    public TimeSpan? GetAverageLapTime(int windowSize)
    {
        if (Laps.Count == 0)
        {
            return null;
        }

        if (windowSize <= 0)
        {
            windowSize = 1;
        }

        var lapsToUse = Laps
            .TakeLast(Math.Min(windowSize, Laps.Count))
            .ToList();

        if (lapsToUse.Count == 0)
        {
            return null;
        }

        var total = TimeSpan.Zero;
        foreach (var lap in lapsToUse)
        {
            total += lap.LapTime;
        }

        return TimeSpan.FromTicks(total.Ticks / lapsToUse.Count);
    }
}

