using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using LastLapCalc.Configuration;
using LastLapCalc.Models;
using LastLapCalc.Services;

namespace LastLapCalc.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private readonly IRaceDataReader _raceDataReader;
    private readonly PredictionEngine _predictionEngine;
    private readonly AppSettings _settings;

    private string _raceElapsedDisplay = "00:00:00";
    private string _raceRemainingDisplay = "00:00:00";
    private string _leaderId = "Leader";
    private string _leaderName = "";
    private string _leaderDescription = "";
    private int _leaderLaps;
    private string _lastLapTimeDisplay = "-";
    private string _averageLapTimeDisplay = "-";
    private int _predictedLapsRemaining;
    private bool _isLastLapNow;
    private bool _showFinishFlag;
    private string _bannerMessage = "";
    private string _predictedFinishDurationDisplay = "-";
    private string _predictedFinishTimeOfDayDisplay = "-";
    private int _averageLapCountUsed = 0;
    private string _xmlFilePath = "";
    private string _statusMessage = "Waiting for race data...";
    private readonly ObservableCollection<LapDisplayInfo> _recentLaps = new ObservableCollection<LapDisplayInfo>();
    private int _lastLapCountWhenBannerShown = -1;
    private int _lastProcessedLapCount = -1;
    private PredictionResult? _lastPrediction;

    // --- Race-complete / finish-line counter state ---
    private bool _raceExpired = false;
    private Dictionary<string, int> _lapsAtRaceExpiry = new();
    private bool _raceComplete = false;
    private int _finishedCount = 0;
    private int _stillRacingCount = 0;
    private int _totalCompetitors = 0;

    public MainViewModel()
    {
        _settings = App.Settings;
        XmlFilePath = _settings.XmlFilePath;
        _raceDataReader = new XmlRaceDataReader(_settings.XmlFilePath);
        _predictionEngine = new PredictionEngine();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_settings.RefreshIntervalMs)
        };
        _timer.Tick += TimerOnTick;
        _timer.Start();
        
        // Read immediately on startup
        TimerOnTick(null, EventArgs.Empty);
    }

    public string RaceElapsedDisplay
    {
        get => _raceElapsedDisplay;
        private set => SetField(ref _raceElapsedDisplay, value);
    }

    public string RaceRemainingDisplay
    {
        get => _raceRemainingDisplay;
        private set => SetField(ref _raceRemainingDisplay, value);
    }

    public string LeaderId
    {
        get => _leaderId;
        private set => SetField(ref _leaderId, value);
    }

    public string LeaderName
    {
        get => _leaderName;
        private set => SetField(ref _leaderName, value);
    }

    /// <summary>
    /// User-editable description of the leader's trike (e.g. make/model/colour).
    /// </summary>
    public string LeaderDescription
    {
        get => _leaderDescription;
        set => SetField(ref _leaderDescription, value);
    }

    public int LeaderLaps
    {
        get => _leaderLaps;
        private set => SetField(ref _leaderLaps, value);
    }

    public string LastLapTimeDisplay
    {
        get => _lastLapTimeDisplay;
        private set => SetField(ref _lastLapTimeDisplay, value);
    }

    public string AverageLapTimeDisplay
    {
        get => _averageLapTimeDisplay;
        private set => SetField(ref _averageLapTimeDisplay, value);
    }

    public int PredictedLapsRemaining
    {
        get => _predictedLapsRemaining;
        private set => SetField(ref _predictedLapsRemaining, value);
    }

    public bool IsLastLapNow
    {
        get => _isLastLapNow;
        private set
        {
            if (SetField(ref _isLastLapNow, value))
            {
                OnPropertyChanged(nameof(ShowBanner));
            }
        }
    }

    public bool ShowFinishFlag
    {
        get => _showFinishFlag;
        private set
        {
            if (SetField(ref _showFinishFlag, value))
            {
                OnPropertyChanged(nameof(ShowBanner));
            }
        }
    }

    public string BannerMessage
    {
        get => _bannerMessage;
        private set => SetField(ref _bannerMessage, value);
    }

    public bool ShowBanner => IsLastLapNow || ShowFinishFlag || _raceComplete;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string PredictedFinishDurationDisplay
    {
        get => _predictedFinishDurationDisplay;
        private set => SetField(ref _predictedFinishDurationDisplay, value);
    }

    public string PredictedFinishTimeOfDayDisplay
    {
        get => _predictedFinishTimeOfDayDisplay;
        private set => SetField(ref _predictedFinishTimeOfDayDisplay, value);
    }

    public int AverageLapCountUsed
    {
        get => _averageLapCountUsed;
        private set => SetField(ref _averageLapCountUsed, value);
    }

    public ObservableCollection<LapDisplayInfo> RecentLaps => _recentLaps;

    public string XmlFilePath
    {
        get => _xmlFilePath;
        private set => SetField(ref _xmlFilePath, value);
    }

    /// <summary>True once the race clock has expired and the banner switches to "RACE COMPLETE".</summary>
    public bool RaceComplete
    {
        get => _raceComplete;
        private set
        {
            if (SetField(ref _raceComplete, value))
            {
                OnPropertyChanged(nameof(ShowBanner));
            }
        }
    }

    /// <summary>Number of teams that have crossed the finish line since the race expired.</summary>
    public int FinishedCount
    {
        get => _finishedCount;
        private set
        {
            if (SetField(ref _finishedCount, value))
                OnPropertyChanged(nameof(FinishCounterDisplay));
        }
    }

    /// <summary>Number of teams still on-track (have not yet crossed since race expired).</summary>
    public int StillRacingCount
    {
        get => _stillRacingCount;
        private set
        {
            if (SetField(ref _stillRacingCount, value))
                OnPropertyChanged(nameof(FinishCounterDisplay));
        }
    }

    /// <summary>Total number of teams in the field (from XML result count).</summary>
    public int TotalCompetitors
    {
        get => _totalCompetitors;
        private set => SetField(ref _totalCompetitors, value);
    }

    /// <summary>One-line summary shown inside the Race Complete banner.</summary>
    public string FinishCounterDisplay =>
        $"Finished: {_finishedCount}  |  Still Racing: {_stillRacingCount}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void TimerOnTick(object? sender, EventArgs e)
    {
        try
        {
            var totalDuration = TimeSpan.FromMinutes(_settings.RaceDurationMinutes);
            RaceState raceState = _raceDataReader.ReadRaceState(totalDuration);

            var predictionSettings = new PredictionSettings
            {
                AverageLapWindow = _settings.AverageLapWindow
            };

            var prediction = _predictionEngine.Compute(raceState, predictionSettings);

            UpdateFromRaceState(raceState, prediction);
            var fileInfo = new System.IO.FileInfo(_settings.XmlFilePath);
            var fileStatus = fileInfo.Exists ? $"✓ {fileInfo.LastWriteTime:HH:mm:ss}" : "✗ Not found";
            StatusMessage = $"Live - {fileStatus}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            // Keep last known values on error, don't reset to zero
        }
    }

    private void UpdateFromRaceState(RaceState state, PredictionResult prediction)
    {
        // Check race expiry first – this gates the banner and finish counter
        UpdateRaceCompleteState(state);

        RaceElapsedDisplay = FormatTime(state.Elapsed);
        RaceRemainingDisplay = FormatTime(state.Remaining);

        LeaderId = state.Leader.Id;
        LeaderName = state.Leader.FullName;
        var currentLapCount = state.Leader.TotalLapsCompleted;
        LeaderLaps = currentLapCount;

        LastLapTimeDisplay = state.Leader.LastLap?.LapTime is { } lastLap
            ? FormatTime(lastLap)
            : "-";

        AverageLapTimeDisplay = prediction.AverageLapTime is { } avg
            ? FormatTime(avg)
            : "-";

        // Calculate how many laps are actually being used for the average
        var windowSize = _settings.AverageLapWindow;
        var actualLapsUsed = Math.Min(windowSize, state.Leader.TotalLaps);
        AverageLapCountUsed = actualLapsUsed;

        // Only update predictions when a new lap is completed
        bool newLapCompleted = currentLapCount > _lastProcessedLapCount && currentLapCount > 0;
        
        if (newLapCompleted)
        {
            // Recalculate predictions based on the last completed lap
            var recalculatedPrediction = RecalculatePredictionFromLastLap(state, prediction);
            _lastPrediction = recalculatedPrediction;
            _lastProcessedLapCount = currentLapCount;
            
            PredictedLapsRemaining = recalculatedPrediction.PredictedLapsRemaining;
            
            // Update banner logic only when lap completes
            UpdateBannerState(recalculatedPrediction, currentLapCount);
            
            // Update predicted finish duration and time of day
            UpdatePredictedFinish(recalculatedPrediction, state);
        }
        else if (_lastPrediction != null)
        {
            // Keep using the last prediction until a new lap completes
            PredictedLapsRemaining = _lastPrediction.PredictedLapsRemaining;
            UpdateBannerState(_lastPrediction, currentLapCount);
        }
        else
        {
            // First time, use the current prediction
            PredictedLapsRemaining = prediction.PredictedLapsRemaining;
            UpdateBannerState(prediction, currentLapCount);
            UpdatePredictedFinish(prediction, state);
        }

        // Update recent laps display (always update this)
        _recentLaps.Clear();
        var recentLapsList = state.Leader.Laps
            .OrderByDescending(l => l.LapNumber)
            .Take(5)
            .ToList();

        foreach (var lap in recentLapsList)
        {
            _recentLaps.Add(new LapDisplayInfo
            {
                LapNumber = lap.LapNumber,
                LapTime = FormatTime(lap.LapTime),
                ElapsedAtCompletion = lap.ElapsedAtCompletion.HasValue ? FormatTime(lap.ElapsedAtCompletion.Value) : "-",
                TimeOfDay = lap.CompletedAtTimeOfDay?.ToString("HH:mm:ss") ?? "-"
            });
        }
    }

    private PredictionResult RecalculatePredictionFromLastLap(RaceState state, PredictionResult basePrediction)
    {
        if (state.Leader.LastLap == null || !basePrediction.AverageLapTime.HasValue)
        {
            return basePrediction;
        }

        // Get the elapsed time when the last lap was completed
        TimeSpan lastLapElapsed = state.Leader.LastLap.ElapsedAtCompletion ?? state.Elapsed;
        
        // Calculate remaining race time from the last lap's perspective
        // Allow negative values (race time has expired)
        TimeSpan remainingFromLastLap = state.TotalDuration - lastLapElapsed;
        
        var avg = basePrediction.AverageLapTime.Value;
        
        // Calculate laps remaining - allow negative remaining time
        int lapsRemaining = 0;
        if (remainingFromLastLap > TimeSpan.Zero && avg > TimeSpan.Zero)
        {
            lapsRemaining = (int)Math.Ceiling(remainingFromLastLap.TotalSeconds / avg.TotalSeconds);
            // Ensure lapsRemaining is never negative
            if (lapsRemaining < 0)
            {
                lapsRemaining = 0;
            }
        }
        
        var isLastLapNow = lapsRemaining <= 2 && lapsRemaining > 0;
        
        return new PredictionResult
        {
            RemainingRaceTime = remainingFromLastLap,
            AverageLapTime = avg,
            PredictedLapsRemaining = lapsRemaining,
            IsLastLapNow = isLastLapNow,
            PredictedFinishTime = lastLapElapsed + TimeSpan.FromSeconds(lapsRemaining * avg.TotalSeconds)
        };
    }

    private void UpdatePredictedFinish(PredictionResult prediction, RaceState state)
    {
        // Predicted finish duration = elapsed time when they finish their last lap
        // This should be based on the last lap's elapsed time + remaining predicted laps * average lap time
        if (prediction.AverageLapTime.HasValue && 
            prediction.PredictedLapsRemaining > 0 &&
            state.Leader.LastLap != null)
        {
            // Calculate predicted finish duration based on last lap's elapsed time
            TimeSpan lastLapElapsed = state.Leader.LastLap.ElapsedAtCompletion ?? state.Elapsed;
            TimeSpan remainingPredictedTime = TimeSpan.FromSeconds(prediction.PredictedLapsRemaining * prediction.AverageLapTime.Value.TotalSeconds);
            TimeSpan predictedFinishDuration = lastLapElapsed + remainingPredictedTime;
            
            PredictedFinishDurationDisplay = FormatTime(predictedFinishDuration);
            
            // Calculate predicted finish time of day
            if (state.Leader.LastLap.CompletedAtTimeOfDay.HasValue)
            {
                var lastLapTimeOfDay = state.Leader.LastLap.CompletedAtTimeOfDay.Value;
                var predictedFinishTimeOfDay = lastLapTimeOfDay.Add(remainingPredictedTime);
                PredictedFinishTimeOfDayDisplay = predictedFinishTimeOfDay.ToString("HH:mm:ss");
            }
            else
            {
                PredictedFinishTimeOfDayDisplay = "-";
            }
        }
        else
        {
            PredictedFinishDurationDisplay = "-";
            PredictedFinishTimeOfDayDisplay = "-";
        }

        // Update recent laps (last 5, newest to oldest)
        _recentLaps.Clear();
        var recentLapsList = state.Leader.Laps
            .OrderByDescending(l => l.LapNumber)
            .Take(5)
            .ToList();

        foreach (var lap in recentLapsList)
        {
            _recentLaps.Add(new LapDisplayInfo
            {
                LapNumber = lap.LapNumber,
                LapTime = FormatTime(lap.LapTime),
                ElapsedAtCompletion = lap.ElapsedAtCompletion.HasValue ? FormatTime(lap.ElapsedAtCompletion.Value) : "-",
                TimeOfDay = lap.CompletedAtTimeOfDay?.ToString("HH:mm:ss") ?? "-"
            });
        }
    }

    private static string FormatTime(TimeSpan timeSpan)
    {
        // Allow negative values - show with minus sign
        if (timeSpan < TimeSpan.Zero)
        {
            var abs = timeSpan.Duration();
            // Use TotalHours cast to int so values >= 24h display correctly (e.g. "24:03:15")
            return $"-{(int)abs.TotalHours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
        }

        // Use TotalHours cast to int to correctly handle races that run >= 24 hours.
        // TimeSpan's "hh" format specifier is capped at 23 and wraps for longer durations.
        return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateBannerState(PredictionResult prediction, int currentLapCount)
    {
        // Race-complete overrides all other banner states
        if (_raceComplete)
        {
            IsLastLapNow = false;
            BannerMessage = "RACE COMPLETE";
            return;
        }

        // If finish flag banner is already showing, keep it
        if (_showFinishFlag)
        {
            IsLastLapNow = false;
            BannerMessage = "PREPARE TO WAVE FINISH FLAG";
            return;
        }

        // Show banner when there are 2 passings remaining (one lap earlier)
        // This means when PredictedLapsRemaining <= 2
        bool shouldShowBanner = prediction.PredictedLapsRemaining <= 2 && prediction.PredictedLapsRemaining > 0;

        if (shouldShowBanner)
        {
            // If we just started showing the last lap board, record the lap count
            if (!_isLastLapNow)
            {
                _lastLapCountWhenBannerShown = currentLapCount;
            }

            IsLastLapNow = true;
            BannerMessage = "SHOW LAST LAP BOARD ON NEXT PASS";

            // Check if a new lap was completed while the banner was showing
            if (_lastLapCountWhenBannerShown >= 0 && currentLapCount > _lastLapCountWhenBannerShown)
            {
                // They passed again - switch to finish flag banner
                IsLastLapNow = false;
                ShowFinishFlag = true;
                BannerMessage = "WAVE CHECKERED FLAG ON NEXT PASS";
            }
        }
        else
        {
            // Not showing banner anymore - reset state (but don't reset if finish flag is showing)
            if (!_showFinishFlag)
            {
                IsLastLapNow = false;
                _lastLapCountWhenBannerShown = -1;
                BannerMessage = "";
            }
        }
    }

    /// <summary>
    /// Called every tick. When the race clock first reaches zero, snapshots each competitor's
    /// lap count. On subsequent ticks, any competitor whose laps increased is counted as
    /// having crossed the finish line.
    /// </summary>
    private void UpdateRaceCompleteState(RaceState state)
    {
        // Only activate once the race clock has expired and we have field data
        if (state.Remaining > TimeSpan.Zero || state.AllCompetitors.Count == 0)
            return;

        // Take a one-time snapshot the first time we see remaining <= 0
        if (!_raceExpired)
        {
            _raceExpired = true;
            _lapsAtRaceExpiry = state.AllCompetitors
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First().Laps);
        }

        // Count finishers (lap count increased since snapshot) vs still racing
        int finished = 0, stillRacing = 0;
        foreach (var competitor in state.AllCompetitors)
        {
            var snapshotLaps = _lapsAtRaceExpiry.GetValueOrDefault(competitor.Id, competitor.Laps);
            if (competitor.Laps > snapshotLaps)
                finished++;
            else
                stillRacing++;
        }

        FinishedCount = finished;
        StillRacingCount = stillRacing;
        TotalCompetitors = state.AllCompetitors.Count;
        RaceComplete = true;
    }
}

public sealed class LapDisplayInfo
{
    public int LapNumber { get; init; }
    public string LapTime { get; init; } = string.Empty;
    public string ElapsedAtCompletion { get; init; } = string.Empty;
    public string TimeOfDay { get; init; } = string.Empty;
}

