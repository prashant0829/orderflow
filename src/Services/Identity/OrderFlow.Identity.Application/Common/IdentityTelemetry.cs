using System.Diagnostics.Metrics;

namespace OrderFlow.Identity.Application.Common;

public static class IdentityTelemetry
{
    public const string MeterName = "OrderFlow.Identity";

    public const string Success = "success";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Locked = "locked";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Logins = Meter.CreateCounter<long>("orderflow.auth.logins", "{login}", "Login attempts by result");
    private static readonly Counter<long> Registrations = Meter.CreateCounter<long>("orderflow.auth.registrations", "{user}", "Accounts created");
    private static readonly Counter<long> TokenReuse = Meter.CreateCounter<long>("orderflow.auth.refresh_token_reuse", "{event}", "Used refresh tokens presented again (possible theft)");

    // Counters start at zero on startup. Prometheus cannot see the first increment of a series that first appears already at 1,
    // so without this the first order of each kind would be missing from rate() and increase().
    static IdentityTelemetry()
    {
        foreach (var result in new[] { Success, InvalidCredentials, Locked })
        {
            Logins.Add(0, new KeyValuePair<string, object?>("result", result));
        }

        Registrations.Add(0);
        TokenReuse.Add(0);
    }

    public static void RecordLogin(string result) => Logins.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void RecordRegistration() => Registrations.Add(1);

    public static void RecordTokenReuse() => TokenReuse.Add(1);
}
