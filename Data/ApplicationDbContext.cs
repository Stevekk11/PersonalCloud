using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalCloud.Models;

namespace PersonalCloud.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public DbSet<Document> Documents { get; set; }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Document>().HasIndex(p => new { p.FileName }).HasDatabaseName("IX_FileName");
    }

}