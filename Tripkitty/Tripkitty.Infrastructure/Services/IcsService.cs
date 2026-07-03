using System.Text;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Services;

public interface IIcsService
{
    string GenerateIcs(Trip trip);
}

public class IcsService : IIcsService
{
    public string GenerateIcs(Trip trip)
    {
        var sb = new StringBuilder();

        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine($"PRODID:-//Tripkitty//Tripkitty//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("X-PUBLISHED-TTL:PT5M");
        sb.AppendLine($"X-WR-CALNAME:{EscapeText(trip.Name)}");

        // Trip-level event if it has dates
        if (trip.Start.HasValue)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:trip-{trip.Id}@tripkitty");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTSTART;VALUE=DATE:{trip.Start.Value:yyyyMMdd}");

            if (trip.End.HasValue)
            {
                // DTEND for all-day is exclusive (next day)
                sb.AppendLine($"DTEND;VALUE=DATE:{trip.End.Value.AddDays(1):yyyyMMdd}");
            }
            else
            {
                sb.AppendLine($"DTEND;VALUE=DATE:{trip.Start.Value.AddDays(1):yyyyMMdd}");
            }

            sb.AppendLine($"SUMMARY:{EscapeText(trip.Name)}");
            sb.AppendLine("END:VEVENT");
        }

        // Individual events
        foreach (var ev in trip.Events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:event-{ev.Id}@tripkitty");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");

            if (ev.Time.HasValue)
            {
                sb.AppendLine($"DTSTART:{ev.Date:yyyyMMdd}T{ev.Time.Value:HHmmss}");
                if (ev.EndTime.HasValue)
                {
                    sb.AppendLine($"DTEND:{ev.Date:yyyyMMdd}T{ev.EndTime.Value:HHmmss}");
                }
                else
                {
                    // Default 1 hour duration
                    var endTime = ev.Time.Value.AddHours(1);
                    sb.AppendLine($"DTEND:{ev.Date:yyyyMMdd}T{endTime:HHmmss}");
                }
            }
            else
            {
                sb.AppendLine($"DTSTART;VALUE=DATE:{ev.Date:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{ev.Date.AddDays(1):yyyyMMdd}");
            }

            sb.AppendLine($"SUMMARY:{EscapeText(ev.Title)}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");

        // Convert to CRLF
        return sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    private static string EscapeText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace(";", "\\;")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }
}
