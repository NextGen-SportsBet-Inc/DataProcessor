using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DataProcessorAPI.Models;

namespace DataProcessorAPI.Data;

public class MatchDbContext : DbContext
{
    public MatchDbContext(DbContextOptions<MatchDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    public DbSet<FootballMatch> FootballMatches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FootballMatch>()
            .Property(e => e.ResultOddsJson)
            .HasColumnName("ResultOdds"); // Use the desired column name
    }
}
