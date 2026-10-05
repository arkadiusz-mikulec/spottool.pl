namespace SpotTool.Web.Domain.Helpers;

public interface IDateTimeProvider
{
    DateTimeOffset Now();
    string ToUserLocalDateTime(DateTimeOffset serverTime, TimeSpan offset, string format = "g");
    DateTimeOffset FormatDateTimeWithoutSecunds(DateTimeOffset input);
}

public class DateTimeProvider : IDateTimeProvider
{
    public string ToUserLocalDateTime(DateTimeOffset serverTime, TimeSpan offsetInMinutes, string format = "g")
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
}