namespace SpotTool.Web.Domain.Helpers;

public interface IDateTimeProvider
{
    DateTimeOffset Now();
    string ToUserLocalTime(DateTimeOffset serverTime, TimeSpan offset, string format = "g");
    DateTimeOffset FormatDateTimeWithoutSecunds(DateTimeOffset input);
    DateTime TodayInUserLocalTime(TimeSpan offsetInMinutes);
}

public class DateTimeProvider : IDateTimeProvider
{
    public string ToUserLocalTime(DateTimeOffset serverTime, TimeSpan offsetInMinutes, string format = "g")
    {
        //Console.WriteLine($"Offset: {offsetInMinutes} - {offsetInMinutes.Minutes}");
        return serverTime.ToOffset(offsetInMinutes).ToString("g");
    }

    public DateTimeOffset Now()
    {
        var now = DateTimeOffset.Now;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute));
    }

    public DateTimeOffset FormatDateTimeWithoutSecunds(DateTimeOffset input)
    {
        return input.AddTicks(-(input.Ticks % TimeSpan.TicksPerMinute));
    }

    public DateTime TodayInUserLocalTime(TimeSpan offsetInMinutes)
    {
        return Now().ToOffset(offsetInMinutes).Date;
    }
}