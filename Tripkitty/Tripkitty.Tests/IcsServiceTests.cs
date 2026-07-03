using Tripkitty.Domain.Entities;
using Tripkitty.Infrastructure.Services;

namespace Tripkitty.Tests;

public class IcsServiceTests
{
    private static Trip MakeTrip(params TripEvent[] events) =>
        new() { Name = "Test", Events = events.ToList() };

    private static TripEvent MakeEvent(string date, string? time, string? endTime) =>
        new()
        {
            Title = "Event",
            Date = DateOnly.Parse(date),
            Time = time is null ? null : TimeOnly.Parse(time),
            EndTime = endTime is null ? null : TimeOnly.Parse(endTime)
        };

    [Fact]
    public void SameDayEvent_EndsOnSameDay()
    {
        var ics = new IcsService().GenerateIcs(MakeTrip(MakeEvent("2026-07-03", "10:00", "12:00")));

        Assert.Contains("DTSTART:20260703T100000", ics);
        Assert.Contains("DTEND:20260703T120000", ics);
    }

    [Fact]
    public void CrossMidnightEvent_EndsOnNextDay()
    {
        var ics = new IcsService().GenerateIcs(MakeTrip(MakeEvent("2026-07-03", "22:00", "01:00")));

        Assert.Contains("DTSTART:20260703T220000", ics);
        Assert.Contains("DTEND:20260704T010000", ics);
    }

    [Fact]
    public void LateEventWithoutEndTime_DefaultHourRollsOverToNextDay()
    {
        var ics = new IcsService().GenerateIcs(MakeTrip(MakeEvent("2026-07-03", "23:30", null)));

        Assert.Contains("DTSTART:20260703T233000", ics);
        Assert.Contains("DTEND:20260704T003000", ics);
    }

    [Fact]
    public void EqualStartAndEndTime_TreatedAsNextDay()
    {
        var ics = new IcsService().GenerateIcs(MakeTrip(MakeEvent("2026-07-03", "10:00", "10:00")));

        Assert.Contains("DTSTART:20260703T100000", ics);
        Assert.Contains("DTEND:20260704T100000", ics);
    }

    [Fact]
    public void AllDayEvent_UsesDateValuesWithExclusiveEnd()
    {
        var ics = new IcsService().GenerateIcs(MakeTrip(MakeEvent("2026-07-03", null, null)));

        Assert.Contains("DTSTART;VALUE=DATE:20260703", ics);
        Assert.Contains("DTEND;VALUE=DATE:20260704", ics);
    }
}
