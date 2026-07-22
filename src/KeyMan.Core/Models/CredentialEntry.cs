using System.Text.Json.Serialization;

namespace KeyMan.Models;

public sealed class CredentialEntry
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateOnly? ExpiresOn { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonIgnore]
    public string ExpirationStatus
    {
        get
        {
            if (ExpiresOn is null)
            {
                return "만료 없음";
            }

            var remainingDays = ExpiresOn.Value.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
            return remainingDays switch
            {
                < 0 => $"{Math.Abs(remainingDays)}일 지남",
                0 => "오늘 만료",
                <= 7 => $"{remainingDays}일 남음",
                _ => ExpiresOn.Value.ToString("yyyy-MM-dd")
            };
        }
    }
}
