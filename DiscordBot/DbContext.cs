using Microsoft.EntityFrameworkCore;
namespace DiscordBot
{
    /// <summary>
    /// AppDBContext establishes the database connection. Inherits from DbContext
    /// </summary>
    public class AppDBContext : DbContext
    {
        public DbSet<DiscordMessage> UserMessages { get; set; }
        public DbSet<DiscordLog> DiscordLog { get; set; }
        public DbSet<BirthdayDef> BirthdayDefs { get; set; }
        public DbSet<DiscordGuildUser> GuildUsers { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlServer(Program.Configuration["connectionString"]);
        }
    }
}
