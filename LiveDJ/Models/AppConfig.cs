namespace LiveDJ.Models;

public record AppConfig
{
    public string? Environment { get; set; } = "DRIFT";
    public string ApiBaseUrl { get; set; } = "https://localhost:5235/"; // change for prod
}
