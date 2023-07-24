using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

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
        public DbSet<Acronym> Acronyms { get; set; }
        public DbSet<GrammarMatch> GrammarMatchs { get; set; }
        public DbSet<GrammarRule> GrammarRule { get; set; }
        public DbSet<Currency> Currencies { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlServer(Program.Configuration["connectionString"]);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GrammarMatch>().Property(e => e.Replacements)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<string?[]>(v, (JsonSerializerOptions)null!)!);
            modelBuilder.Entity<GrammarRule>().Property(e => e.Urls)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<string?[]>(v, (JsonSerializerOptions)null!)!);

            var valueComparer = new ValueComparer<string[]>((x,y) => x!.SequenceEqual(y!), c=> c.Aggregate(0, (a,v) => HashCode.Combine(a, v.GetHashCode())),
                c  => c.ToArray());

            modelBuilder.Entity<GrammarMatch>().Property(e => e.Replacements).Metadata.SetValueComparer(valueComparer);
            modelBuilder.Entity<GrammarRule>().Property(e => e.Urls).Metadata.SetValueComparer(valueComparer);

            base.OnModelCreating(modelBuilder);
        }
    }
}
