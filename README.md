# Last Lap Timer

A WPF desktop application for Windows that reads live-updating XML race data, tracks the race leader, and predicts remaining laps with a last lap board indicator.

## Requirements

- .NET 10 SDK
- Windows (win-x64)

## Build

```bash
dotnet build LastLapCalc/LastLapCalc.csproj
```

## Run

```bash
dotnet run --project LastLapCalc/LastLapCalc.csproj
```

## Publish (single-file executable)

```bash
dotnet publish LastLapCalc/LastLapCalc.csproj -c Release
```

Output will be in `LastLapCalc/bin/Release/net10.0-windows/win-x64/publish/`.

## Configuration

Edit `LastLapCalc/appsettings.json` to configure:

- XML file path for race data
- Refresh interval
- Lap averaging window for predictions

## License

See project for license details.
