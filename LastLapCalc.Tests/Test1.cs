using System;
using LastLapCalc.Models;
using LastLapCalc.Services;

namespace LastLapCalc.Tests;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void ConstantPace_PredictsCorrectLastLap()
    {
        // 10-minute race, leader runs consistent 1-minute laps.
        var state = new RaceState
        {
            TotalDuration = TimeSpan.FromMinutes(10),
            Elapsed = TimeSpan.FromMinutes(5),
            Leader = new CompetitorState()
        };

        for (var i = 1; i <= 5; i++)
        {
            state.Leader.Laps.Add(new LapInfo
            {
                LapNumber = i,
                LapTime = TimeSpan.FromMinutes(1)
            });
        }

        var engine = new PredictionEngine();
        var settings = new PredictionSettings
        {
            AverageLapWindow = 3
        };

        var result = engine.Compute(state, settings);

        Assert.AreEqual(5, result.PredictedLapsRemaining);
        Assert.IsFalse(result.IsLastLapNow);
    }
}
