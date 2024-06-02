using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DataProcessorAPI.Models;

namespace DataProcessorAPI.Data;

public sealed class ProcessorDbContext : DbContext // Can go wrong cause of the sealed keyword
{
    public ProcessorDbContext(DbContextOptions<ProcessorDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }
    
    public DbSet<FootballOdd> FootballOdds { get; set; }
    
    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     modelBuilder.Entity<FootballOdd>().ToTable("FootballOdds");
    // }
}