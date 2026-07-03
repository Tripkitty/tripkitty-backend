namespace Tripkitty.Application.DTOs;

public record AddEventRequest(string Title, string Date, string? Time, string? EndTime);
