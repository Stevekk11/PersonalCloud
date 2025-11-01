namespace PersonalCloud.Models;

/// <summary>
/// Represents a document with an associated signature status.
/// </summary>
/// <remarks>
/// This class is designed to encapsulate a document and its signed status,
/// indicating whether the document has been signed as a PDF.
/// </remarks>
public class DocumentWithSignature
{
    public Document Document { get; set; }
    public bool IsPdfSigned { get; set; }
}