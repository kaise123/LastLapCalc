using System;
using LastLapCalc.Models;

namespace LastLapCalc.Services;

public sealed class PredictionSettings
{
    /// <summary>
    /// Number of most recent laps to include when computing the average.
    /// </summary>
    public int AverageLapWindow { get; init; } = 3;

    /// <summary>
    /// Optional minimum average lap time to avoid unrealistically small values.
    /// </summary>
    public TimeSpan? MinimumAverageLapTime { get; init; }
}

public sealed class PredictionResult
{
    public bool IsLastLapNow { get; init; }
    public int PredictedLapsRemaining { get; init; }
    public TimeSpan RemainingRaceTime { get; init; }
    public TimeSpan? AverageLapTime { get; init; }
    public TimeSpan? PredictedFinishTime { get; init; }
}

/// <summary>
/// Encapsulates the logic for predicting remaining laps and detecting the last lap.
/// </summary>
public sealed class PredictionEngine
{
    public PredictionResult Compute(RaceState state, PredictionSettings settings)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var remaining = state.Remaining;
        var averageLap = state.Leader.GetAverageLapTime(settings.AverageLapWindow);

        // Allow negative remaining time (race time has expired)
        if (averageLap is null || averageLap.Value <= TimeSpan.Zero)
        {
            return new PredictionResult
            {
                RemainingRaceTime = remaining,
                AverageLapTime = averageLap,
                PredictedLapsRemaining = 0,
                IsLastLapNow = false,
                PredictedFinishTime = null
            };
        }

        var avg = averageLap.Value;

        if (settings.MinimumAverageLapTime.HasValue &&
            avg < settings.MinimumAverageLapTime.Value)
        {
            avg = settings.MinimumAverageLapTime.Value;
        }

        int lapsRemaining = 0;
        if (remaining > TimeSpan.Zero)
        {
            lapsRemaining = (int)Math.Ceiling(remaining.TotalSeconds / avg.TotalSeconds);
            // Ensure lapsRemaining is never negative
            if (lapsRemaining < 0)
            {
                lapsRemaining = 0;
            }
        }

        var isLastLapNow = state.Leader.TotalLaps > 0 && lapsRemaining <= 2 && lapsRemaining > 0;
        var predictedFinishTime = state.Elapsed + TimeSpan.FromSeconds(lapsRemaining * avg.TotalSeconds);

        return new PredictionResult
        {
            RemainingRaceTime = remaining,
            AverageLapTime = avg,
            PredictedLapsRemaining = lapsRemaining,
            IsLastLapNow = isLastLapNow,
            PredictedFinishTime = predictedFinishTime
        };
    }
}

