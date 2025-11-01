using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PersonalCloud.Models;

/// <summary>
/// Represents a document entity that is associated with a user and contains metadata about a stored file.
/// </summary>
/// <remarks>
/// This class is primarily used for managing file information, including its storage path, metadata, and upload timestamp.
/// It leverages Entity Framework Core annotations for database schema creation.
/// </remarks>
public class Document
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(500)]
    public string? FileName { get; set; }
    [Required, MaxLength(100)]
    public string? ContentType { get; set; }
    [Required]
    public long FileSize { get; set; }
    [Required]
    public string? StoragePath { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? LoginId { get; set; }
    [ForeignKey("LoginId")]
    public IdentityUser? User { get; set; }
}