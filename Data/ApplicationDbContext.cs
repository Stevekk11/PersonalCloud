using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalCloud.Models;

namespace PersonalCloud.Data;

/// <summary>
/// Represents the application's database context, providing access to the database
/// and managing entities such as user identities and custom models.
/// </summary>
/// <remarks>
/// This class extends <see cref="Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext"/>,
/// enabling integration with ASP.NET Core Identity for user authentication and authorization.
/// Additionally, it includes a DbSet for managing <see cref="PersonalCloud.Models.Document"/> entities.
/// </remarks>
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