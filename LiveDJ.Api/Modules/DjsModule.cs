using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

public static class DjsModule
{
    // Helper: for now we “fake” the current DJ by header; later we’ll use Firebase auth
    private static string GetCurrentDjId(HttpContext ctx)
        => ctx.Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
           ? id.ToString()
           : "dj-1"; // default for quick testing

    public static IEndpointRouteBuilder MapDjsModule(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/djs").WithTags("DJs");

        // Public: list DJs (for BookingPage ComboBox)
        g.MapGet("", async (IDjsRepository repo) =>
        {
            var list = await repo.ListAsync();
            return Results.Ok(list.Select(d => new { id = d.Id, name = d.Name }));
        });

        // Public: DJ details (availability + pricing)
        g.MapGet("/{id}", async (string id, IDjsRepository repo) =>
        {
            var dj = await repo.GetAsync(id);
            return dj is null ? Results.NotFound() : Results.Ok(new
            {
                id = dj.Id,
                name = dj.Name,
                availability = dj.Availability,
                pricing = dj.Pricing
            });
        });

        // Public: human-readable availability summary (used by sample UI)
        g.MapGet("/{id}/availability", async (string id, IDjsRepository repo) =>
        {
            var dj = await repo.GetAsync(id);
            if (dj?.Availability is null) return Results.Ok("No availability set.");
            var a = dj.Availability;
            var days = string.Join(", ", a.Days);
            var range = $"{a.StartHour:00}:00–{a.EndHour:00}";
            return Results.Ok($"Available {days} {range}");
        });

        // “Me” endpoints (UNO calls these). For now, use X-User-Id header to pick the DJ.
        var me = g.MapGroup("/me");

        // GET current settings
        me.MapGet("", async (HttpContext ctx, IDjsRepository repo) =>
        {
            var id = GetCurrentDjId(ctx);
            var dj = await repo.GetAsync(id);
            return dj is null ? Results.NotFound() : Results.Ok(dj);
        });

        // PUT availability
        me.MapPut("/availability", async (HttpContext ctx, IDjsRepository repo, AvailabilityModel req) =>
        {
            var id = GetCurrentDjId(ctx);
            await repo.UpdateAvailabilityAsync(id, req);
            return Results.NoContent();
        });

        // PUT pricing
        me.MapPut("/pricing", async (HttpContext ctx, IDjsRepository repo, PricingModel req) =>
        {
            var id = GetCurrentDjId(ctx);
            await repo.UpdatePricingAsync(id, req);
            return Results.NoContent();
        });

        return app;
    }
}
