
using PersonalCloud.Models;

namespace PersonalCloud.Models;

/// <summary>
/// Represents the view model for displaying a list of documents.
/// This class is specifically designed to be used as the model in views
/// where a collection of documents needs to be presented or managed.
/// </summary>
public class DocumentViewModel
{
    public IList<Document> Documents { get; set; } = new List<Document>();
    public List<DocumentWithSignature> DocumentsWithSignature { get; set; } = new();

    /// <summary>The current folder being viewed, or null for the root.</summary>
    public Folder? CurrentFolder { get; set; }

    /// <summary>Immediate sub-folders of the current folder.</summary>
    public List<Folder> SubFolders { get; set; } = new();

    /// <summary>Breadcrumb trail from root to the current folder (inclusive).</summary>
    public List<Folder> Breadcrumb { get; set; } = new();

    /// <summary>All folders owned by the user (for move-to-folder dropdown).</summary>
    public List<Folder> AllFolders { get; set; } = new();
}