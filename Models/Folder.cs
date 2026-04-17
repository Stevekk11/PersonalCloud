using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCloud.Models;

/// <summary>
/// Represents a folder entity that can contain documents and sub-folders,
/// forming a hierarchical folder tree per user.
/// </summary>
public class Folder
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public int? ParentFolderId { get; set; }

    [ForeignKey("ParentFolderId")]
    public Folder? ParentFolder { get; set; }

    public ICollection<Folder> SubFolders { get; set; } = new List<Folder>();

    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public string? LoginId { get; set; }

    [ForeignKey("LoginId")]
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
