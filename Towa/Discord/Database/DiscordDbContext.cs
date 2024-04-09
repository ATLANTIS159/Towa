using Microsoft.EntityFrameworkCore;
using Towa.Discord.Models;

namespace Towa.Discord.Database;

public class DiscordDbContext : DbContext
{
    public DiscordDbContext(DbContextOptions<DiscordDbContext> options) : base(options)
    {
    }

    public DbSet<GiveawayItem> GiveawayItems { get; set; } = null!;
}