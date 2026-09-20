# Last Lap Timer - Changelog

## [Unreleased]

### Fixed
- Predicted finish duration (and all elapsed/remaining time displays) now correctly show durations ≥ 24 hours instead of wrapping back to 00:xx:xx. The `hh` TimeSpan format specifier was replaced with an integer cast of `TotalHours` so overnight 24-hour races display the correct accumulated hours.

### Added
- Trike description text field in the Leader panel. Operators can type a free-text description (e.g. make, model, colour) of the leading trike during the race.

---

## Version 1.0 - 17.02.26

### Initial Release

#### Core Features
- WPF desktop application for Windows (.NET 10)
- Reads live-updating XML race data file
- Tracks race leader and predicts remaining laps
- Displays last lap board indicator when appropriate

#### Data Management
- XML file reader with live updates (configurable refresh interval)
- Tracks up to 10 recent lap times (XML only provides last 3)
- Maintains lap history across reads to accumulate full history
- Parses race time in MM:SS and HH:MM:SS formats
- Parses lap times in seconds and MM:SS.mmm formats

#### Prediction Engine
- Calculates average lap time from configurable window (default: 3 laps)
- Predicts remaining passings based on remaining race time
- Predictions recalculate only when a lap completes (prevents drift)
- Predictions based on last completed lap's elapsed time
- Supports negative remaining time (continues counting after race duration expires)

#### UI Features
- Race time display (elapsed and remaining)
- Leader information:
  - Trike number
  - Total laps completed (from XML)
  - First and last name
- Prediction panel:
  - Last lap time
  - Average lap time
  - Number of laps used for average calculation
  - Predicted passings remaining
  - Predicted finish duration
  - Predicted finish time of day
- Recent laps table (last 5, newest to oldest):
  - Lap number
  - Lap time
  - Elapsed time at completion
  - Time of day
- Banner indicators:
  - "SHOW LAST LAP BOARD" - shown when 2 passings remaining
  - "PREPARE TO WAVE FINISH FLAG" - shown after competitor passes again during last lap

#### Configuration
- `appsettings.json` configuration file:
  - Race duration (minutes)
  - XML file path
  - Average lap window (number of laps for average)
  - Refresh interval (milliseconds)

#### Bug Fixes & Improvements
- Fixed race time parsing to support HH:MM:SS format
- Fixed elapsed time calculation to only update when laps complete
- Fixed total laps display to read from XML instead of tracked lap count
- Fixed prediction drift by recalculating only on lap completion
- Fixed layout overlap issues in prediction panel
- Improved error handling and status messages
- Added XML file path display in status area

#### Technical Details
- C# WPF application (.NET 10.0-windows)
- MVVM pattern with INotifyPropertyChanged
- LINQ to XML for parsing
- DispatcherTimer for periodic updates
- FileSystemWatcher-safe XML reading (FileShare.ReadWrite)
- Unit tests for prediction engine

---

## Development Notes

### Architecture
- **Models**: RaceState, CompetitorState, LapInfo
- **Services**: XmlRaceDataReader, PredictionEngine
- **ViewModels**: MainViewModel with property change notifications
- **Configuration**: AppSettings with JSON deserialization

### XML Format Support
- Parses `resultspage` XML format
- Extracts race time from `<label type="racetime">`
- Extracts leader data from `<result position="1">`
- Parses lap times from `lasttime`, `secondlasttime`, `thirdlasttime` attributes
- Parses time of day from `lasttimeofday` attribute

### Prediction Logic
- Predictions update only when `TotalLapsCompleted` increases
- Uses last lap's `ElapsedAtCompletion` as base for calculations
- Prevents prediction drift by fixing to completed lap times
- Banner state only changes on lap completion
