
using PersonalCloud.Models;

namespace PersonalCloud.Models;

/// <summary>
/// Represents the view model for displaying a list of documents.
/// This class is specifically designed to be used as the model in views
/// where a collection of documents needs to be presented or managed.
/// </summary>
public class DocumentViewModel
{
    public IList<Document> Documents { get; set; }
    public List<DocumentWithSignature> DocumentsWithSignature { get; set; } = new();
}