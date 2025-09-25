using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

public static class CheckoutModule
{
    public static IEndpointRouteBuilder MapCheckoutModule(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Checkout");

        // ---- PRICE QUOTE -----------------------------------------------------
        // Example: GET /api/quotes?djId=dj-1&durationHours=4
        g.MapGet("/quotes", async (string djId, int durationHours, IDjsRepository repo) =>
        {
            var dj = await repo.GetAsync(djId);
            if (dj is null) return Results.NotFound("DJ not found.");
            if (dj.Pricing is null) return Results.BadRequest("DJ has no pricing set.");

            var hours = Math.Max(durationHours, dj.Pricing.MinHours);
            var total = hours * dj.Pricing.HourlyRateCents;

            return Results.Ok(new
            {
                currency = "usd",
                totalCents = total,
                hoursBilled = hours
            });
        });

        // ---- CHECKOUT CREATE -------------------------------------------------
        // Example: POST /api/checkout/create { djId, eventDate, startTime, durationHours }
        // Returns a URL that your UNO app opens. This version is a MOCK so you can test now.
        g.MapPost("/checkout/create", async (CreateCheckoutRequest req, IDjsRepository repo) =>
        {
            var dj = await repo.GetAsync(req.DjId);
            if (dj is null) return Results.NotFound("DJ not found.");
            if (dj.Pricing is null) return Results.BadRequest("DJ has no pricing set.");

            var hours = Math.Max(req.DurationHours, dj.Pricing.MinHours);
            var total = hours * dj.Pricing.HourlyRateCents;

            // Platform fee example (10%)
            var platformFeeCents = (int)Math.Round(total * 0.10m);

            // MOCK: pick a processor deterministically so your UI branch works
            var processor = PickMockProcessor(dj.Id); // "stripe" or "paypal"

            // MOCK URLs to launch in UNO (replace with real ones when you add Stripe/PayPal)
            var mockUrl = processor == "stripe"
                ? $"https://example.test/stripe/checkout?dj={Uri.EscapeDataString(dj.Id)}&total={total}"
                : $"https://example.test/paypal/approve?dj={Uri.EscapeDataString(dj.Id)}&total={total}";

            // TODO (when you add real payments):
            // - If Stripe: create Checkout Session with destination charge and return session.Url
            // - If PayPal: create Order and return approval link
            // - Create a pending booking record here and include bookingId in the response

            return Results.Ok(new
            {
                processor,                    // "stripe" or "paypal"
                checkoutUrl = processor == "stripe" ? mockUrl : null,
                approvalUrl = processor == "paypal" ? mockUrl : null,
                currency = "usd",
                totalCents = total,
                platformFeeCents
            });
        });

        return app;
    }

    // Simple deterministic pick so different DJs can simulate different flows
    private static string PickMockProcessor(string djId)
        => (Math.Abs(djId.GetHashCode()) % 2 == 0) ? "stripe" : "paypal";

    public sealed class CreateCheckoutRequest
    {
        public required string DjId { get; set; }
        public required string EventDate { get; set; }   
        public required string StartTime { get; set; }   
        public int DurationHours { get; set; }           
    }
}
