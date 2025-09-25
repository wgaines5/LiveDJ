namespace LiveDJ.Models;

public sealed class DjDetails
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DjAvailability? Availability { get; set; }
    public DjPricing? Pricing { get; set; }
}

public sealed class DjAvailability
{
    public List<string> Days { get; set; } = new();
    public int StartHour { get; set; }
    public int EndHour { get; set; }
}

public sealed class DjPricing
{
    public int HourlyRateCents { get; set; }
    public int MinHours { get; set; }
}
