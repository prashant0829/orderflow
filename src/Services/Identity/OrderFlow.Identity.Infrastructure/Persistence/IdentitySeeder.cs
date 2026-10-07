using Microsoft.EntityFrameworkCore;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Domain.Users;
using OrderFlow.Security;

namespace OrderFlow.Identity.Infrastructure.Persistence;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = "admin@orderflow.local";
    public string AdminPassword { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = "customer@orderflow.local";
    public string CustomerPassword { get; set; } = string.Empty;
}

/// <summary>Creates a demo admin and customer. Does nothing unless passwords are configured, so production never gets default accounts.</summary>
public static class IdentitySeeder
{
    /// <summary>Same id the demo scripts always used as the customer id.</summary>
    public static readonly Guid DemoCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoAdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(IdentityDbContext dbContext, IPasswordHasher hasher, SeedOptions options, TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await AddIfMissingAsync(dbContext, hasher, DemoAdminId, options.AdminEmail, options.AdminPassword, "Demo Admin", Roles.Admin, now, cancellationToken);
        await AddIfMissingAsync(dbContext, hasher, DemoCustomerId, options.CustomerEmail, options.CustomerPassword, "Demo Customer", Roles.Customer, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddIfMissingAsync(
        IdentityDbContext dbContext, IPasswordHasher hasher, Guid id, string email, string password, string name, string role, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password) || await dbContext.Users.AnyAsync(user => user.Id == id, cancellationToken))
        {
            return;
        }

        dbContext.Users.Add(User.Create(id, email, hasher.Hash(password), name, role, now));
    }
}
