using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.Modules.Identity.Domain;

namespace ShopFlow.Modules.Identity.Infrastructure;

internal class IdentitySeeder : IModuleSeeder
{
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeeder> _logger;
    private readonly TimeProvider _timeProvider;

    public IdentitySeeder(IdentityDbContext dbContext, IConfiguration configuration, ILogger<IdentitySeeder> logger, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding Identity...");

        var adminEmail = _configuration["Admin:Email"];
        var adminPassword = _configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogInformation("Admin seed configuration not found. Skipping.");
            return;
        }

        adminEmail = adminEmail.ToLowerInvariant();

        if (!await _dbContext.Users.AnyAsync(u => u.Email == adminEmail, cancellationToken))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "Admin",
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _dbContext.Users.Add(adminUser);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Admin user seeded successfully.");
        }
        else
        {
            _logger.LogInformation("Admin user already exists. Skipping.");
        }
    }
}
