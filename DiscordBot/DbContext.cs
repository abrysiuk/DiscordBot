using Microsoft.EntityFrameworkCore;
namespace DiscordBot
{
    public class AppDBContext : DbContext
    {
        public DbSet<DiscordMessage> UserMessages { get; set; }
        public DbSet<SlashLog> SlashLogs { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlServer(Program.Configuration["connectionString"]);
        }
    }
}
