using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LastLapCalc.Models;

namespace LastLapCalc.Services;

/// <summary>
/// Reads race data for the leader from a live-updating XML file.
/// Parses the resultspage XML format with labels and results elements.
/// Maintains a rolling history of up to 10 laps by tracking lap completions across reads.
/// </summary>
public sealed class XmlRaceDataReader : IRaceDataReader
{
    private readonly string _xmlFilePath;
    private int _lastSeenLapCount = 0;
    private readonly List<LapInfo> _lapHistory = new List<LapInfo>();
    private const int MaxLapHistory = 10;

    public XmlRaceDataReader(string xmlFilePath)
    {
        _xmlFilePath = xmlFilePath ?? throw new ArgumentNullException(nameof(xmlFilePath));
    }

    public RaceState ReadRaceState(TimeSpan totalDuration)
    {
        var state = new RaceState
        {
            TotalDuration = totalDuration
        };

        if (!File.Exists(_xmlFilePath))
        {
            // No data yet – treat as race not started.
            throw new FileNotFoundException($"XML file not found: {_xmlFilePath}");
        }

        try
        {
            using var stream = File.Open(_xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var doc = XDocument.Load(stream);

            var root = doc.Root;
            if (root is null)
            {
                return state;
            }

            // Parse race elapsed time from <label type="racetime">MM:SS</label>
            var raceTimeLabel = root.Descendants("label")
                .FirstOrDefault(l => l.Attribute("type")?.Value == "racetime");
            
            if (raceTimeLabel != null && !string.IsNullOrWhiteSpace(raceTimeLabel.Value))
            {
                var raceTimeValue = raceTimeLabel.Value.Trim();
                state.Elapsed = ParseRaceTime(raceTimeValue);
                
                // If parsing failed, throw an exception to help debug
                if (state.Elapsed == TimeSpan.Zero && raceTimeValue != "00:00" && raceTimeValue != "0:00:00")
                {
                    throw new InvalidOperationException($"Failed to parse race time: '{raceTimeValue}'");
                }
            }
            else
            {
                throw new InvalidOperationException("Race time label not found in XML");
            }

            // Find the leader (first result with position="1")
            var leaderResult = root.Descendants("result")
                .FirstOrDefault(r => r.Attribute("position")?.Value == "1");

            if (leaderResult != null)
            {
                // Get leader identifier (competitor number)
                var leaderNo = leaderResult.Attribute("no")?.Value;
                state.Leader.Id = !string.IsNullOrWhiteSpace(leaderNo) ? leaderNo : "Leader";
                
                // Get first and last name
                state.Leader.FirstName = leaderResult.Attribute("firstname")?.Value ?? string.Empty;
                state.Leader.LastName = leaderResult.Attribute("lastname")?.Value ?? string.Empty;

                // Get total laps completed from XML
                var lapsAttr = leaderResult.Attribute("laps")?.Value;
                if (int.TryParse(lapsAttr, out var totalLaps))
                {
                    // Store the total laps completed from XML
                    state.Leader.TotalLapsCompleted = totalLaps;
                    // Parse lap times from lasttime, secondlasttime, thirdlasttime attributes
                    // These are in seconds (e.g., "59.409") or MM:SS format (e.g., "1:07.298")
                    var lastTimeStr = leaderResult.Attribute("lasttime")?.Value;
                    var secondLastTimeStr = leaderResult.Attribute("secondlasttime")?.Value;
                    var thirdLastTimeStr = leaderResult.Attribute("thirdlasttime")?.Value;
                    
                    // Parse time of day for the most recent lap (XML only provides lasttimeofday)
                    var lastTimeOfDayStr = leaderResult.Attribute("lasttimeofday")?.Value;
                    var lastTimeOfDay = ParseTimeOfDay(lastTimeOfDayStr);

                    // Extract the 3 most recent laps from XML with time of day
                    // Note: Only the most recent lap has timeOfDay in XML
                    var xmlLaps = new List<(int lapNumber, TimeSpan lapTime, DateTime? timeOfDay)>();

                    if (!string.IsNullOrWhiteSpace(thirdLastTimeStr) && 
                        ParseLapTime(thirdLastTimeStr) is { } thirdLastTime &&
                        totalLaps >= 3)
                    {
                        xmlLaps.Add((totalLaps - 2, thirdLastTime, null));
                    }

                    if (!string.IsNullOrWhiteSpace(secondLastTimeStr) && 
                        ParseLapTime(secondLastTimeStr) is { } secondLastTime &&
                        totalLaps >= 2)
                    {
                        xmlLaps.Add((totalLaps - 1, secondLastTime, null));
                    }

                    if (!string.IsNullOrWhiteSpace(lastTimeStr) && 
                        ParseLapTime(lastTimeStr) is { } lastTime &&
                        totalLaps >= 1)
                    {
                        xmlLaps.Add((totalLaps, lastTime, lastTimeOfDay));
                    }

                    // Update lap history: merge XML data with stored history
                    UpdateLapHistory(xmlLaps, totalLaps, state.Elapsed);

                    // Copy the last MaxLapHistory laps to the race state
                    var lapsToInclude = _lapHistory
                        .OrderBy(l => l.LapNumber)
                        .TakeLast(MaxLapHistory)
                        .ToList();

                    foreach (var lap in lapsToInclude)
                    {
                        state.Leader.Laps.Add(new LapInfo
                        {
                            LapNumber = lap.LapNumber,
                            LapTime = lap.LapTime,
                            CompletedAtUtc = lap.CompletedAtUtc,
                            CompletedAtTimeOfDay = lap.CompletedAtTimeOfDay,
                            ElapsedAtCompletion = lap.ElapsedAtCompletion
                        });
                    }
                }
            }

            return state;
        }
        catch (Exception ex)
        {
            // On parse errors, return the last-known-good basic state.
            // Re-throw to allow caller to see the error
            throw new InvalidOperationException($"Failed to read race data from '{_xmlFilePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Updates the internal lap history by merging XML-provided laps with stored history.
    /// Maintains up to MaxLapHistory (10) laps, keeping the most recent ones.
    /// Accumulates lap history across multiple reads since XML only provides the last 3 laps.
    /// </summary>
    private void UpdateLapHistory(List<(int lapNumber, TimeSpan lapTime, DateTime? timeOfDay)> xmlLaps, int totalLaps, TimeSpan currentElapsed)
    {
        // If this is the first read or lap count decreased (race reset?), rebuild from XML
        if (_lastSeenLapCount == 0 || totalLaps < _lastSeenLapCount)
        {
            _lapHistory.Clear();
            foreach (var (lapNumber, lapTime, timeOfDay) in xmlLaps.OrderBy(l => l.lapNumber))
            {
                // Calculate elapsed time at completion (approximate based on current elapsed and lap times)
                TimeSpan? elapsedAtCompletion = CalculateElapsedAtCompletion(lapNumber, totalLaps, currentElapsed, lapTime);
                
                _lapHistory.Add(new LapInfo
                {
                    LapNumber = lapNumber,
                    LapTime = lapTime,
                    CompletedAtTimeOfDay = timeOfDay,
                    ElapsedAtCompletion = elapsedAtCompletion
                });
            }
            _lastSeenLapCount = totalLaps;
            return;
        }

        // Remove any laps that are no longer valid (lap number > totalLaps)
        _lapHistory.RemoveAll(l => l.LapNumber > totalLaps);

        // Check if a new lap was completed
        bool newLapCompleted = totalLaps > _lastSeenLapCount;
        
        // Update or add laps from XML (these are the most recent 3 laps)
        foreach (var (lapNumber, lapTime, timeOfDay) in xmlLaps)
        {
            var existingIndex = _lapHistory.FindIndex(l => l.LapNumber == lapNumber);
            
            if (existingIndex >= 0)
            {
                // Update existing lap (time may have been corrected in XML)
                var existingLap = _lapHistory[existingIndex];
                
                // Only recalculate elapsed time if this is a new lap OR if we're rebuilding
                // For existing laps, preserve the elapsed time when they were completed
                TimeSpan? elapsedAtCompletion = existingLap.ElapsedAtCompletion;
                
                // If this is the most recent lap and it's new, calculate elapsed time
                if (newLapCompleted && lapNumber == totalLaps)
                {
                    elapsedAtCompletion = currentElapsed;
                }
                
                _lapHistory[existingIndex] = new LapInfo
                {
                    LapNumber = lapNumber,
                    LapTime = lapTime,
                    CompletedAtUtc = existingLap.CompletedAtUtc,
                    CompletedAtTimeOfDay = timeOfDay ?? existingLap.CompletedAtTimeOfDay,
                    ElapsedAtCompletion = elapsedAtCompletion
                };
            }
            else
            {
                // New lap - add it (this happens when totalLaps increased)
                // Calculate elapsed time: previous lap's elapsed + this lap's time
                TimeSpan? elapsedAtCompletion = null;
                
                if (lapNumber == totalLaps)
                {
                    // Most recent lap: use current elapsed time
                    elapsedAtCompletion = currentElapsed;
                }
                else
                {
                    // Older lap: calculate from previous lap's elapsed time
                    var previousLap = _lapHistory
                        .Where(l => l.LapNumber < lapNumber)
                        .OrderByDescending(l => l.LapNumber)
                        .FirstOrDefault();
                    
                    if (previousLap != null && previousLap.ElapsedAtCompletion.HasValue)
                    {
                        elapsedAtCompletion = previousLap.ElapsedAtCompletion.Value + lapTime;
                    }
                }
                
                _lapHistory.Add(new LapInfo
                {
                    LapNumber = lapNumber,
                    LapTime = lapTime,
                    CompletedAtTimeOfDay = timeOfDay,
                    ElapsedAtCompletion = elapsedAtCompletion
                });
            }
        }

        // Sort by lap number and keep only the last MaxLapHistory laps
        _lapHistory.Sort((a, b) => a.LapNumber.CompareTo(b.LapNumber));
        var minLapToKeep = Math.Max(1, totalLaps - MaxLapHistory + 1);
        _lapHistory.RemoveAll(l => l.LapNumber < minLapToKeep);

        _lastSeenLapCount = totalLaps;
    }

    /// <summary>
    /// Parses race time in MM:SS or HH:MM:SS format.
    /// Examples: "31:39" = 31 minutes 39 seconds, "1:04:42" = 1 hour 4 minutes 42 seconds.
    /// </summary>
    private static TimeSpan ParseRaceTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.Zero;
        }

        var parts = value.Trim().Split(':');
        
        // Handle HH:MM:SS format (3 parts)
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes) &&
            int.TryParse(parts[2], out var seconds))
        {
            return new TimeSpan(hours, minutes, seconds);
        }
        
        // Handle MM:SS format (2 parts)
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out minutes) &&
            int.TryParse(parts[1], out seconds))
        {
            return TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(seconds));
        }

        // Try standard TimeSpan parsing as fallback
        if (TimeSpan.TryParse(value, out var ts))
        {
            return ts;
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Parses lap time which can be in seconds (e.g., "59.409") or MM:SS.mmm format (e.g., "1:07.298").
    /// Returns null if parsing fails.
    /// </summary>
    private static TimeSpan? ParseLapTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        // Try parsing as seconds first (e.g., "59.409")
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Try parsing as MM:SS.mmm format (e.g., "1:07.298")
        var parts = value.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var minutes) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
        {
            return TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(secs));
        }

        // Try standard TimeSpan parsing as fallback
        if (TimeSpan.TryParse(value, out var ts))
        {
            return ts;
        }

        return null;
    }

    /// <summary>
    /// Parses time of day string (e.g., "22:23:34.387").
    /// Returns null if parsing fails.
    /// </summary>
    private static DateTime? ParseTimeOfDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try parsing as HH:mm:ss.fff format
        if (DateTime.TryParseExact(value.Trim(), "HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt;
        }

        // Try without milliseconds
        if (DateTime.TryParseExact(value.Trim(), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return dt;
        }

        // Try standard parsing
        if (DateTime.TryParse(value, out dt))
        {
            return dt;
        }

        return null;
    }

    /// <summary>
    /// Calculates the elapsed race time when a lap was completed.
    /// For the most recent lap, uses current elapsed time.
    /// For older laps, calculates by subtracting subsequent lap times from current elapsed.
    /// </summary>
    private TimeSpan? CalculateElapsedAtCompletion(int lapNumber, int totalLaps, TimeSpan currentElapsed, TimeSpan lapTime)
    {
        if (totalLaps == 0)
        {
            return null;
        }

        // If this is the most recent lap, use current elapsed time
        if (lapNumber == totalLaps)
        {
            return currentElapsed;
        }

        // For older laps, calculate by subtracting subsequent lap times from current elapsed
        // Sum all lap times after this lap
        var subsequentLapTime = TimeSpan.Zero;
        foreach (var lap in _lapHistory.Where(l => l.LapNumber > lapNumber && l.LapNumber <= totalLaps).OrderBy(l => l.LapNumber))
        {
            subsequentLapTime += lap.LapTime;
        }
        
        // Also include the current lap time if we're calculating for a lap before the most recent
        if (lapNumber < totalLaps)
        {
            // Estimate: subtract subsequent lap times from current elapsed
            var elapsed = currentElapsed - subsequentLapTime;
            return elapsed > TimeSpan.Zero ? elapsed : null;
        }

        return null;
    }
}

