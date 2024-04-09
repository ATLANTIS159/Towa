using Microsoft.EntityFrameworkCore;
using Towa.ChatGpt.Models.Database;

namespace Towa.ChatGpt.Database;

public class ChatGptDbContext : DbContext
{
    public ChatGptDbContext(DbContextOptions<ChatGptDbContext> options) : base(options)
    {
    }

    public DbSet<Chat> Chats { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>()
            .HasMany(m => m.Messages)
            .WithOne(n => n.Chat)
            .HasForeignKey(k => k.UserId)
            .HasPrincipalKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}