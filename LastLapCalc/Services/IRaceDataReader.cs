using System;
using LastLapCalc.Models;

namespace LastLapCalc.Services;

public interface IRaceDataReader
{
    /// <summary>
    /// Reads the latest race state for the leader from the underlying data source.
    /// </summary>
    RaceState ReadRaceState(TimeSpan totalDuration);
}

