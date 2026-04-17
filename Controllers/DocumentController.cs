using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalCloud.Models;
using PersonalCloud.Services;
using Microsoft.AspNetCore.Identity;
using PersonalCloud.Data;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using SixLabors.ImageSharp;
using Syncfusion.XlsIO;
using Syncfusion.XlsIORenderer;
using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;

namespace PersonalCloud.Controllers;

/// <summary>
/// Controller class responsible for managing user documents.
/// Provides functionality for uploading, downloading, deleting documents,
/// and retrieving image details or image gallery views.
/// </summary>
[Authorize]
public class DocumentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly DocumentService _documentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(ApplicationDbContext context,
        DocumentService documentService, ILogger<DocumentController> logger, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _documentService = documentService;
        _logger = logger;
        _userManager = userManager;
    }

    /// <summary>
    /// Retrieves the currently authenticated user's unique identifier based on their username and user type.
    /// </summary>
    /// <returns>A string representing the unique identifier of the currently authenticated user.</returns>
    /// <exception cref="Exception">Thrown if the current user's information cannot be found in the database.</exception>
    private string GetCurrentUserId()
    {
        var user = _userManager.GetUserAsync(User).Result;
        return user?.Id ?? throw new Exception("User not found");
    }

    /// <summary>
    /// Displays the user's document management dashboard, listing all documents associated
    /// with the currently authenticated user, optionally filtered by folder.
    /// </summary>
    /// <param name="folderId">The ID of the folder to view, or null for the root.</param>
    /// <returns>A view displaying the user's documents and associated data.</returns>
    /// <exception cref="Exception">Thrown if the authenticated user's information cannot be retrieved.</exception>
    public async Task<IActionResult> Index(int? folderId = null)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} is viewing folder {FolderId}.", userId, folderId?.ToString() ?? "root");

        var docs = await _documentService.GetDocumentsInFolderAsync(userId, folderId);
        var documentsWithSignature = docs.Select(doc => new DocumentWithSignature
        {
            Document = doc,
            IsPdfSigned = false
        }).ToList();

        var subFolders = await _documentService.GetSubFoldersAsync(userId, folderId);
        var breadcrumb = await _documentService.GetBreadcrumbAsync(folderId, userId);
        var allFolders = await _documentService.GetAllUserFoldersAsync(userId);
        Folder? currentFolder = folderId.HasValue ? await _documentService.GetFolderAsync(folderId.Value, userId) : null;

        return View(new DocumentViewModel
        {
            DocumentsWithSignature = documentsWithSignature,
            CurrentFolder = currentFolder,
            SubFolders = subFolders,
            Breadcrumb = breadcrumb,
            AllFolders = allFolders
        });
    }


    /// <summary>
    /// Handles the file upload process for the currently authenticated user. The uploaded file is stored and associated with the user's account.
    /// </summary>
    /// <param name="file">The file being uploaded by the user.</param>
    /// <param name="folderId">Optional folder ID to upload the file into.</param>
    /// <returns>An <see cref="IActionResult"/> that redirects to the index page after the file is successfully uploaded or returns an error if the upload fails.</returns>
    /// <exception cref="Exception">Thrown if no file is selected for upload or if an error occurs during file processing.</exception>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadFile(IFormFile file, int? folderId = null)
    {
        if (file == null || file.Length == 0)
        {
            TempData["UploadError"] = "No file selected.";
            return RedirectToAction(nameof(Index), new { folderId });
        }

        var userId = GetCurrentUserId();
        try
        {
            await _documentService.AddDocumentToFolderAsync(userId, file, folderId);
            _logger.LogInformation($"User {User.Identity.Name} uploaded a new document with name {file.FileName}.");
        }
        catch (ArgumentException ex)
        {
            TempData["UploadError"] = ex.Message;
            _logger.LogWarning(ex, "Upload blocked for user {User} with file {FileName}", User.Identity.Name,
                file.FileName);
            return RedirectToAction(nameof(Index), new { folderId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["UploadError"] = ex.Message;
            _logger.LogWarning(ex, "Storage limit exceeded for user {User} with file {FileName}", User.Identity.Name,
                file.FileName);
            return RedirectToAction(nameof(Index), new { folderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while uploading file {FileName} for user {User}", file.FileName,
                User.Identity.Name);
            TempData["UploadError"] = "An unexpected error occurred while uploading the file.";
            return RedirectToAction(nameof(Index), new { folderId });
        }

        return RedirectToAction(nameof(Index), new { folderId });
    }

    /// <summary>
    /// Handles the download process for a requested document by its unique identifier.
    /// Validates user permissions and ensures the file exists before serving it.
    /// </summary>
    /// <param name="id">The unique identifier of the document to be downloaded.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the requested file for download
    /// if successful, or an appropriate HTTP status code if an error occurs.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist on the storage system.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not authorized to access the document.</exception>
    /// <exception cref="Exception">Thrown for any unexpected errors occurring during the download process.</exception>
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var doc = await _documentService.GetDocumentAsync(id, userId);
            if (doc == null)
                return NotFound();

            var absolutePath = Path.GetFullPath(doc.StoragePath);
            if (!System.IO.File.Exists(absolutePath))
                return NotFound();

            var mimeType = doc.ContentType ?? "application/octet-stream";
            
            // Security: Prevent execution of dangerous types by forcing download/nosniff
            var dangerousExtensions = new[] { ".cs", ".exe", ".dll", ".config", ".json", ".cshtml", ".js", ".html", ".htm", ".cmd", ".bat", ".vbs", ".ps1" };
            var extension = Path.GetExtension(doc.FileName).ToLower();
            
            if (dangerousExtensions.Contains(extension))
            {
                mimeType = "application/octet-stream";
            }

            _logger.LogInformation($"User {User.Identity.Name} downloaded document with the name {doc.FileName}.");
            return PhysicalFile(absolutePath, mimeType, doc.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while downloading document {DocumentId}", id);
            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Retrieves and displays a filtered list of image documents uploaded by the current user.
    /// Only documents with supported image content types (e.g., jpeg, png, gif, etc.) are included,
    /// and they are sorted in descending order based on their upload date.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> rendering the user's image gallery within a
    /// <see cref="DocumentViewModel"/>.</returns>
    /// <exception cref="Exception">Thrown if the current user's information cannot be retrieved.</exception>
    public async Task<IActionResult> Gallery()
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} is viewing image gallery.", userId);
        var imageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/bmp" };

        var images = await _context.Documents
            .Where(d => d.LoginId == userId && imageTypes.Contains(d.ContentType.ToLower()))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return View(new DocumentViewModel { Documents = images });
    }

    public async Task<IActionResult> Music()
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} is viewing music library.", userId);
        var audioTypes = new[]
            { "audio/mpeg", "audio/mp3", "audio/wav", "audio/x-wav", "audio/x-ms-wma", "audio/x-ms-wax", "audio/ogg" };

        var audioFiles = await _context.Documents
            .Where(d => d.LoginId == userId && audioTypes.Contains(d.ContentType.ToLower()))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
        return View(new DocumentViewModel { Documents = audioFiles });
    }

    /// <summary>
    /// Retrieves detailed information about a specific image document for the current user.
    /// If the specified document does not exist or is not an image, a 404 Not Found result is returned.
    /// </summary>
    /// <param name="id">The unique identifier of the document.</param>
    /// <returns>An <see cref="IActionResult"/> containing the image details in JSON format,
    /// or a NotFound result if the document is unavailable or not an image.</returns>
    /// <exception cref="Exception">Thrown if the user cannot be identified or document retrieval fails.</exception>
    [HttpGet]
    public async Task<IActionResult> GetImageDetails(int id)
    {
        var userId = GetCurrentUserId();
        _logger.LogDebug("Fetching image details for document {DocumentId} and user {UserId}", id, userId);
        var doc = await _documentService.GetDocumentAsync(id, userId);

        if (doc == null || !doc.ContentType.StartsWith("image/"))
        {
            _logger.LogWarning("Image details requested for non-existent or non-image document {DocumentId} by user {UserId}", id, userId);
            return NotFound();
        }

        var absolutePath = Path.GetFullPath(doc.StoragePath);
        using var image = await Image.LoadAsync(absolutePath);

        return Json(new
        {
            id = doc.Id,
            fileName = doc.FileName,
            fileSize = FormatFileSize(doc.FileSize),
            uploadedAt = doc.UploadedAt.ToString("dd.MM.yyyy HH:mm"),
            contentType = doc.ContentType,
            downloadUrl = Url.Action("Download", new { id = doc.Id }),
            width = image.Width,
            height = image.Height
        });
    }

    /// <summary>
    /// Streams the specified document as a file for inline browser preview.
    /// </summary>
    /// <param name="id">The unique identifier of the document to preview.</param>
    /// <returns>An <see cref="IActionResult"/> containing the file for preview if found, or a NotFound result if the document does not exist.</returns>
    /// <exception cref="Exception">Thrown when the current authenticated user's information cannot be found.</exception>
    [HttpGet]
    public async Task<IActionResult> Preview(int id)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} requested preview for document {DocumentId}", userId, id);
        var doc = await _documentService.GetDocumentAsync(id, userId);
        if (doc == null)
            return NotFound();

        var mimeType = doc.ContentType ?? "application/octet-stream";
        var absolutePath = Path.GetFullPath(doc.StoragePath);

        if (!System.IO.File.Exists(absolutePath))
            return NotFound();

        // Security: Prevent preview/execution of dangerous types
        var dangerousExtensions = new[] { ".exe", ".dll", ".config", ".cshtml", ".html", ".htm", ".cmd", ".bat", ".vbs", ".ps1" };
        var extension = Path.GetExtension(doc.FileName).ToLower();
        
        Response.Headers.Append("X-Content-Type-Options", "nosniff");

        if (dangerousExtensions.Contains(extension))
        {
            return Forbid("Preview of this file type is disabled for security reasons.");
        }

        // Handle Word documents
        if (mimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ||
            mimeType == "application/msword")
        {
            using (Stream fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read))
            {
                // Load the Word document
                using (WordDocument wordDocument = new WordDocument(fileStream, Syncfusion.DocIO.FormatType.Docx))
                {
                    // Create a new DocIORenderer instance
                    using (DocIORenderer renderer = new DocIORenderer())
                    {
                        // Convert Word document to PDF
                        using (PdfDocument pdfDocument = renderer.ConvertToPDF(wordDocument))
                        {
                            MemoryStream outputStream = new MemoryStream();
                            pdfDocument.Save(outputStream);
                            outputStream.Position = 0;

                            // Return PDF for preview
                            return File(outputStream.ToArray(), "application/pdf");
                        }
                    }
                }
            }
        }
        // Handle Excel documents
        else if (mimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                 mimeType == "application/vnd.ms-excel")
        {
            using (ExcelEngine excelEngine = new ExcelEngine())
            {
                IApplication application = excelEngine.Excel;
                using (Stream fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read))
                {
                    IWorkbook workbook = application.Workbooks.Open(fileStream);
                    XlsIORenderer renderer = new XlsIORenderer();

                    using (PdfDocument pdfDocument = renderer.ConvertToPDF(workbook))
                    {
                        MemoryStream outputStream = new MemoryStream();
                        pdfDocument.Save(outputStream);
                        outputStream.Position = 0;
                        return File(outputStream.ToArray(), "application/pdf");
                    }
                }
            }
        }
        // Handle PowerPoint documents
        else if (mimeType == "application/vnd.openxmlformats-officedocument.presentationml.presentation" ||
                 mimeType == "application/vnd.ms-powerpoint")
        {
            using (Stream fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read))
            {
                using (IPresentation presentation = Presentation.Open(fileStream))
                {
                    using (PdfDocument pdfDocument = PresentationToPdfConverter.Convert(presentation))
                    {
                        MemoryStream outputStream = new MemoryStream();
                        pdfDocument.Save(outputStream);
                        outputStream.Position = 0;
                        return File(outputStream.ToArray(), "application/pdf");
                    }
                }
            }
        }

        // For all other file types, use the existing preview logic
        // Ensure text files are served as text/plain to be previewable but not executable
        if (mimeType.StartsWith("text/") || extension == ".cs" || extension == ".js" || extension == ".json")
        {
            mimeType = "text/plain";
        }

        return PhysicalFile(absolutePath, mimeType, enableRangeProcessing: true);
    }


    /// <summary>
    /// Formats the file size to be human readable.
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Deletes a specific document from the user's account.
    /// </summary>
    /// <param name="id">The id of the to be deleted document</param>
    /// <param name="folderId">The current folder ID to redirect back to</param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int? folderId = null)
    {
        var userId = GetCurrentUserId();
        await _documentService.DeleteDocumentAsync(id, userId);
        _logger.LogInformation($"User {User.Identity.Name} deleted document with ID {id}.");
        return RedirectToAction(nameof(Index), new { folderId });
    }

    /// <summary>
    /// Renames a document.
    /// </summary>
    /// <param name="id">The document ID</param>
    /// <param name="newName">The new name for the document</param>
    /// <param name="folderId">The current folder ID to redirect back to</param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int id, string newName, int? folderId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _documentService.RenameDocumentAsync(id, userId, newName);
            
            if (success)
            {
                TempData["UploadSuccess"] = "File renamed successfully.";
                _logger.LogInformation($"User {User.Identity.Name} renamed document {id} to {newName}.");
            }
            else
            {
                TempData["UploadError"] = "Failed to rename file.";
            }
        }
        catch (ArgumentException ex)
        {
            TempData["UploadError"] = ex.Message;
            _logger.LogWarning(ex, "Failed to rename document {DocumentId} for user {User}", id, User.Identity.Name);
        }
        catch (Exception ex)
        {
            TempData["UploadError"] = "An unexpected error occurred while renaming the file.";
            _logger.LogError(ex, "Unexpected error while renaming document {DocumentId} for user {User}", id, User.Identity.Name);
        }

        return RedirectToAction(nameof(Index), new { folderId });
    }

    /// <summary>
    /// Moves a document to a specified folder by folder ID.
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="targetFolderId">The target folder ID (null = root)</param>
    /// <param name="currentFolderId">The current folder ID to redirect back to</param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToFolder(int documentId, int? targetFolderId, int? currentFolderId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _documentService.MoveDocumentToFolderByIdAsync(documentId, userId, targetFolderId);
            
            if (success)
            {
                TempData["UploadSuccess"] = "File moved successfully.";
                _logger.LogInformation($"User {User.Identity.Name} moved document {documentId} to folder {targetFolderId?.ToString() ?? "(root)"}.");
            }
            else
            {
                TempData["UploadError"] = "Failed to move file.";
            }
        }
        catch (ArgumentException ex)
        {
            TempData["UploadError"] = ex.Message;
            _logger.LogWarning(ex, "Failed to move document {DocumentId} for user {User}", documentId, User.Identity.Name);
        }
        catch (Exception ex)
        {
            TempData["UploadError"] = "An unexpected error occurred while moving the file.";
            _logger.LogError(ex, "Unexpected error while moving document {DocumentId} for user {User}", documentId, User.Identity.Name);
        }

        return RedirectToAction(nameof(Index), new { folderId = currentFolderId });
    }

    /// <summary>
    /// AJAX endpoint: moves a document to a folder (used for drag-and-drop).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveFileApi(int documentId, int? targetFolderId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _documentService.MoveDocumentToFolderByIdAsync(documentId, userId, targetFolderId);
            if (success)
            {
                _logger.LogInformation("User {User} drag-dropped document {DocId} to folder {FolderId}",
                    User.Identity!.Name, documentId, targetFolderId?.ToString() ?? "(root)");
                return Json(new { success = true });
            }
            return Json(new { success = false, error = "Failed to move file." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving document {DocumentId} via drag-drop", documentId);
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the list of folders for the current user as JSON.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetFolders()
    {
        var userId = GetCurrentUserId();
        var folders = await _documentService.GetAllUserFoldersAsync(userId);
        return Json(folders.Select(f => new { f.Id, f.Name, f.ParentFolderId }));
    }

    /// <summary>
    /// Creates a new folder for the current user.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFolder(string name, int? parentFolderId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _documentService.CreateFolderAsync(userId, name, parentFolderId);
            TempData["UploadSuccess"] = $"Folder '{name}' created.";
            _logger.LogInformation("User {User} created folder '{Name}' under parent {ParentId}", User.Identity!.Name, name, parentFolderId?.ToString() ?? "(root)");
        }
        catch (ArgumentException ex)
        {
            TempData["UploadError"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["UploadError"] = "An unexpected error occurred while creating the folder.";
            _logger.LogError(ex, "Error creating folder '{Name}' for user {User}", name, User.Identity!.Name);
        }

        return RedirectToAction(nameof(Index), new { folderId = parentFolderId });
    }

    /// <summary>
    /// Renames a folder.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameFolder(int id, string newName, int? parentFolderId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _documentService.RenameFolderAsync(id, userId, newName);
            if (success)
            {
                TempData["UploadSuccess"] = "Folder renamed successfully.";
                _logger.LogInformation("User {User} renamed folder {Id} to '{NewName}'", User.Identity!.Name, id, newName);
            }
            else
            {
                TempData["UploadError"] = "Failed to rename folder.";
            }
        }
        catch (ArgumentException ex)
        {
            TempData["UploadError"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["UploadError"] = "An unexpected error occurred while renaming the folder.";
            _logger.LogError(ex, "Error renaming folder {Id} for user {User}", id, User.Identity!.Name);
        }

        return RedirectToAction(nameof(Index), new { folderId = parentFolderId });
    }

    /// <summary>
    /// Deletes a folder. Documents inside are moved to the parent folder.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFolder(int id, int? parentFolderId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _documentService.DeleteFolderAsync(id, userId);
            TempData["UploadSuccess"] = "Folder deleted. Files were moved to the parent folder.";
            _logger.LogInformation("User {User} deleted folder {Id}", User.Identity!.Name, id);
        }
        catch (Exception ex)
        {
            TempData["UploadError"] = "An unexpected error occurred while deleting the folder.";
            _logger.LogError(ex, "Error deleting folder {Id} for user {User}", id, User.Identity!.Name);
        }

        return RedirectToAction(nameof(Index), new { folderId = parentFolderId });
    }}
