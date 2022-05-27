using System.Text.Json;
using K1P1.RowCounterBot.Database.Infrastructure;
using K1P1.RowCounterBot.Models;
using Microsoft.EntityFrameworkCore;

namespace K1P1.RowCounterBot.Database;

public class DefaultDbContext : DbContext
{
    public DbSet<Counter> Counters { get; set; } = null!;

    public DbSet<StateMachine> StateMachines { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var filename = Path.Join(Environment.GetEnvironmentVariable(Constants.DbPathEnvVariable), "bot.sqlite");
        optionsBuilder.UseSqlite($"Filename={filename}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Counter>(x =>
        {
            x.HasKey(counter => new {counter.Id, counter.ChatId});
            
            x.Property(c => c.Id).ValueGeneratedNever();
            
            x.Property(c => c.ChatId).ValueGeneratedNever();

            x.HasIndex(counter => counter.ChatId);
        });
        
        modelBuilder.Entity<StateMachine>(x =>
        {
            x.HasKey(s => s.ChatId);
            x.Property(s => s.ChatId)
                .ValueGeneratedNever();
        });
    }
}