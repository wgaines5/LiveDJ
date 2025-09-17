namespace LiveDJ.Models;

public record AppConfig
{
    public string? Environment { get; set; } = "DRIFT";
    public string ApiBaseUrl { get; set; } = "https://localhost:5235/"; // change for prod

    public string FirebaseApiKey { get; set; } = "AIzaSyAC72VHybyaG8VuKgqV940o8X88lFGmzEw";
    public string FirebaseBaseUrl { get; set; } = "https://identitytoolkit.googleapis.com/v1"; // Auth REST base
    public string SecureTokenUrl { get; set; } = "https://securetoken.googleapis.com/v1"; // token refresh base
}
