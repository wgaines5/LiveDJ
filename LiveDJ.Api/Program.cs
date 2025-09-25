
var builder = WebApplication.CreateBuilder(args);

// CORS (dev: wide open; tighten for prod)
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true)));



// In-memory repo for quick start
builder.Services.AddSingleton<IDjsRepository, InMemoryDjsRepository>();

var app = builder.Build();

app.UseCors();


// Map endpoints (see DjsModule.cs)
app.MapDjsModule();

app.Run();

app.MapCheckoutModule();

// ====== Contracts ======
public interface IDjsRepository
{
    Task<List<DjModel>> ListAsync();
    Task<DjModel?> GetAsync(string id);
    Task UpdateAvailabilityAsync(string id, AvailabilityModel availability);
    Task UpdatePricingAsync(string id, PricingModel pricing);
}

// ====== Models ======
public sealed record DjModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public AvailabilityModel? Availability { get; init; }
    public PricingModel? Pricing { get; init; }
}

public sealed record AvailabilityModel
{
    public required List<string> Days { get; init; } = new(); // "friday","saturday"
    public required int StartHour { get; init; }             // 0..23
    public required int EndHour { get; init; }               // 0..23
}

public sealed record PricingModel
{
    public required int HourlyRateCents { get; init; }
    public required int MinHours { get; init; }
}

// ====== In-memory repository (seeded) ======
public sealed class InMemoryDjsRepository : IDjsRepository
{
    private readonly Dictionary<string, DjModel> _store = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dj-1"] = new DjModel
        {
            Id = "dj-1",
            Name = "DJ Drift",
            Availability = new AvailabilityModel { Days = new() { "friday", "saturday" }, StartHour = 20, EndHour = 2 },
            Pricing = new PricingModel { HourlyRateCents = 15000, MinHours = 2 }
        },
        ["dj-2"] = new DjModel
        {
            Id = "dj-2",
            Name = "DJ Echo",
            Availability = new AvailabilityModel { Days = new() { "thursday", "sunday" }, StartHour = 18, EndHour = 23 },
            Pricing = new PricingModel { HourlyRateCents = 12000, MinHours = 2 }
        }
    };

    public Task<List<DjModel>> ListAsync() => Task.FromResult(_store.Values.ToList());

    public Task<DjModel?> GetAsync(string id)
        => Task.FromResult(_store.TryGetValue(id, out var dj) ? dj : null);

    public Task UpdateAvailabilityAsync(string id, AvailabilityModel availability)
    {
        if (_store.TryGetValue(id, out var dj))
            _store[id] = dj with { Availability = availability };
        return Task.CompletedTask;
    }

    public Task UpdatePricingAsync(string id, PricingModel pricing)
    {
        if (_store.TryGetValue(id, out var dj))
            _store[id] = dj with { Pricing = pricing };
        return Task.CompletedTask;
    }
}
