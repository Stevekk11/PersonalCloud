using Microsoft.EntityFrameworkCore;
using PersonalCloud.Data;
using PersonalCloud.Models;

namespace PersonalCloud.Services;

/// <summary>
/// Provides services for managing documents, including adding, retrieving, and deleting user-specific documents.
/// </summary>
public class DocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly string _storageRoot;
    private readonly ILogger<DocumentService> _logger;

    /// <summary>
    /// Maximum storage allowed per user in bytes (10 GB / 50GB).
    /// </summary>
    public const long MaxStoragePerUser = 10L * 1024 * 1024 * 1024;
    public const long MaxStoragePerPremiumUser = 50L * 1024 * 1024 * 1024;

    public DocumentService(ApplicationDbContext context, string storageRoot, ILogger<DocumentService> logger)
    {
        _context = context;
        _storageRoot = storageRoot;
        _logger = logger;
        Directory.CreateDirectory(_storageRoot);
    }

    /// <summary>
    /// Asynchronously calculates the total storage used by a specific user.
    /// </summary>
    /// <param name="loginId">The unique identifier of the user.</param>
    /// <returns>A task representing the asynchronous operation, containing the total bytes used by the user.</returns>
    public async Task<long> GetUserStorageUsedAsync(string loginId)
    {
        return await _context.Documents
            .Where(d => d.LoginId == loginId)
            .SumAsync(d => d.FileSize);
    }

    /// <summary>
    /// Asynchronously adds a new document to the database and stores the file in the specified storage location.
    /// </summary>
    /// <param name="loginId">The unique identifier of the user who is uploading the document.</param>
    /// <param name="file">The uploaded file to be stored, including file metadata such as name, content type, and size.</param>
    /// <returns>A task representing the asynchronous operation, with a Document object that contains the details of the uploaded document.</returns>
    public async Task<Document> AddDocumentAsync(string loginId, IFormFile file)
    {
        var disallowedExtensions = new[]
        {
            ".cs", ".exe",".cshtml",".js"
        };

        var fileExtension = Path.GetExtension(file.FileName).ToLower();

        if (disallowedExtensions.Contains(fileExtension))
        {
            _logger.LogWarning("File upload blocked: disallowed extension {Extension} for user {UserId}", fileExtension, loginId);
            throw new ArgumentException($"File type '{fileExtension}' is not allowed for security reasons.");
        }

        // Check storage limit
        var currentUsage = await GetUserStorageUsedAsync(loginId);
        if (currentUsage + file.Length > MaxStoragePerUser)
        {
            _logger.LogWarning("File upload blocked: storage limit exceeded for user {UserId}", loginId);
            throw new InvalidOperationException("Storage limit exceeded. You have reached the maximum storage capacity of 10 GB.");
        }

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
        var storagePath = Path.Combine(_storageRoot, fileName);

        using (var stream = new FileStream(storagePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var document = new Document
        {
            LoginId = loginId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Document {DocumentId} added for user {UserId}", document.Id, loginId);
        return document;
    }

    /// <summary>
    /// Asynchronously retrieves a list of documents associated with the specified user.
    /// </summary>
    /// <param name="loginId">The unique identifier of the user whose documents are being retrieved.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of Document objects.</returns>
    public async Task<List<Document>> GetUserDocumentsAsync(string loginId)
    {
        return await _context.Documents.Where(d => d.LoginId == loginId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<Document> GetDocumentAsync(int documentId, string loginId)
    {
        return await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.LoginId.Equals(loginId) ) ??
               throw new InvalidOperationException();
    }

    /// <summary>
    /// Asynchronously deletes a document from the database and its corresponding file from the storage location.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document to be deleted.</param>
    /// <param name="loginId">The unique identifier of the user attempting to delete the document to ensure proper ownership validation.</param>
    /// <returns>A task representing the asynchronous operation, returning a boolean value indicating whether the deletion was successful or not.</returns>
    public async Task<bool> DeleteDocumentAsync(int documentId, string loginId)
    {
        var document = await GetDocumentAsync(documentId, loginId);
        if (document == null)
        {
            _logger.LogWarning("Attempted to delete non-existent document {DocumentId} for user {UserId}", documentId, loginId);
            return false;
        }

        // Only allow deletion within _storageRoot
        var fullStorageRoot = Path.GetFullPath(_storageRoot);
        var sanitizedPath = Path.GetFullPath(document.StoragePath);

        // Ensure document.StoragePath is under the storage root
        if (!sanitizedPath.StartsWith(fullStorageRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Attempted path traversal detected for user {UserId} on document {DocumentId}", loginId, documentId);
            throw new UnauthorizedAccessException("Attempted path traversal detected.");
        }

        if (File.Exists(sanitizedPath))
        {
            File.Delete(sanitizedPath);
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Document {DocumentId} deleted for user {UserId}", documentId, loginId);
        return true;
    }

    public async Task<Document?> GetLatestUserDocumentAsync(string loginId)
    {
        return await _context.Documents
            .Where(d => d.LoginId == loginId)
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Renames a document's display name (FileName property).
    /// </summary>
    /// <param name="documentId">The unique identifier of the document to rename.</param>
    /// <param name="loginId">The unique identifier of the user who owns the document.</param>
    /// <param name="newFileName">The new name for the document.</param>
    /// <returns>A task representing the asynchronous operation, returning true if successful.</returns>
    public async Task<bool> RenameDocumentAsync(int documentId, string loginId, string newFileName)
    {
        if (string.IsNullOrWhiteSpace(newFileName))
        {
            throw new ArgumentException("File name cannot be empty.", nameof(newFileName));
        }

        // Sanitize the filename to prevent path traversal
        var sanitizedFileName = Path.GetFileName(newFileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName) || sanitizedFileName != newFileName)
        {
            throw new ArgumentException("Invalid file name.", nameof(newFileName));
        }

        var document = await GetDocumentAsync(documentId, loginId);
        if (document == null)
        {
            _logger.LogWarning("Attempted to rename non-existent document {DocumentId} for user {UserId}", documentId, loginId);
            return false;
        }

        document.FileName = sanitizedFileName;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Document {DocumentId} renamed to {NewFileName} for user {UserId}", documentId, sanitizedFileName, loginId);
        return true;
    }

    /// <summary>
    /// Gets a list of unique folder paths for a user.
    /// </summary>
    /// <param name="loginId">The unique identifier of the user.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of folder paths.</returns>
    public async Task<List<string>> GetUserFoldersAsync(string loginId)
    {
        return await _context.Documents
            .Where(d => d.LoginId == loginId && !string.IsNullOrEmpty(d.FolderPath))
            .Select(d => d.FolderPath!)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync();
    }

    /// <summary>
    /// Moves a document to a specified folder.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document to move.</param>
    /// <param name="loginId">The unique identifier of the user who owns the document.</param>
    /// <param name="folderPath">The folder path to move the document to. Use empty string or null for root.</param>
    /// <returns>A task representing the asynchronous operation, returning true if successful.</returns>
    public async Task<bool> MoveDocumentToFolderAsync(int documentId, string loginId, string? folderPath)
    {
        // Sanitize folder path to prevent path traversal
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            var sanitizedPath = folderPath.Replace("\\", "/").Trim('/');
            if (sanitizedPath.Contains("..") || sanitizedPath.Contains("~"))
            {
                throw new ArgumentException("Invalid folder path.", nameof(folderPath));
            }
            folderPath = sanitizedPath;
        }
        else
        {
            folderPath = null;
        }

        var document = await GetDocumentAsync(documentId, loginId);
        if (document == null)
        {
            _logger.LogWarning("Attempted to move non-existent document {DocumentId} for user {UserId}", documentId, loginId);
            return false;
        }

        document.FolderPath = folderPath;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Document {DocumentId} moved to folder {FolderPath} for user {UserId}", documentId, folderPath ?? "(root)", loginId);
        return true;
    }

    // -----------------------------------------------------------------------
    // Folder management (new ID-based system)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new folder for the user.
    /// </summary>
    public async Task<Folder> CreateFolderAsync(string loginId, string name, int? parentFolderId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name cannot be empty.", nameof(name));

        // Sanitize folder name
        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', '.' }).Distinct().ToArray();
        if (name.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Folder name contains invalid characters.", nameof(name));

        // Verify parent folder belongs to user
        if (parentFolderId.HasValue)
        {
            var parent = await _context.Folders.FirstOrDefaultAsync(f => f.Id == parentFolderId && f.LoginId == loginId);
            if (parent == null)
                throw new ArgumentException("Parent folder not found.", nameof(parentFolderId));
        }

        // Check for duplicate name in same parent
        var exists = await _context.Folders.AnyAsync(f =>
            f.LoginId == loginId &&
            f.ParentFolderId == parentFolderId &&
            f.Name == name);
        if (exists)
            throw new ArgumentException($"A folder named '{name}' already exists here.");

        var folder = new Folder
        {
            Name = name,
            ParentFolderId = parentFolderId,
            LoginId = loginId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Folder '{FolderName}' (Id={FolderId}) created for user {UserId}", name, folder.Id, loginId);
        return folder;
    }

    /// <summary>
    /// Deletes a folder. All documents inside are moved to the parent folder (or root).
    /// Sub-folders are also deleted recursively.
    /// </summary>
    public async Task<bool> DeleteFolderAsync(int folderId, string loginId)
    {
        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.LoginId == loginId);
        if (folder == null)
        {
            _logger.LogWarning("Attempted to delete non-existent folder {FolderId} for user {UserId}", folderId, loginId);
            return false;
        }

        await DeleteFolderRecursiveAsync(folder, loginId);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Folder {FolderId} deleted for user {UserId}", folderId, loginId);
        return true;
    }

    private async Task DeleteFolderRecursiveAsync(Folder folder, string loginId)
    {
        // Move documents to parent folder
        var docs = await _context.Documents.Where(d => d.FolderId == folder.Id && d.LoginId == loginId).ToListAsync();
        foreach (var doc in docs)
        {
            doc.FolderId = folder.ParentFolderId;
        }

        // Recurse into sub-folders
        var subFolders = await _context.Folders.Where(f => f.ParentFolderId == folder.Id && f.LoginId == loginId).ToListAsync();
        foreach (var sub in subFolders)
        {
            // Reparent sub-folders to this folder's parent
            sub.ParentFolderId = folder.ParentFolderId;
        }

        _context.Folders.Remove(folder);
    }

    /// <summary>
    /// Renames a folder.
    /// </summary>
    public async Task<bool> RenameFolderAsync(int folderId, string loginId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Folder name cannot be empty.", nameof(newName));

        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', '.' }).Distinct().ToArray();
        if (newName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Folder name contains invalid characters.", nameof(newName));

        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.LoginId == loginId);
        if (folder == null) return false;

        // Check for duplicate in same parent
        var exists = await _context.Folders.AnyAsync(f =>
            f.LoginId == loginId &&
            f.ParentFolderId == folder.ParentFolderId &&
            f.Name == newName &&
            f.Id != folderId);
        if (exists)
            throw new ArgumentException($"A folder named '{newName}' already exists here.");

        folder.Name = newName;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Folder {FolderId} renamed to '{NewName}' for user {UserId}", folderId, newName, loginId);
        return true;
    }

    /// <summary>
    /// Gets the immediate sub-folders of a given folder (or root if folderId is null).
    /// </summary>
    public async Task<List<Folder>> GetSubFoldersAsync(string loginId, int? parentFolderId)
    {
        return await _context.Folders
            .Where(f => f.LoginId == loginId && f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all folders for a user (flat list for move-to-folder dropdown).
    /// </summary>
    public async Task<List<Folder>> GetAllUserFoldersAsync(string loginId)
    {
        return await _context.Folders
            .Where(f => f.LoginId == loginId)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a single folder by ID, ensuring it belongs to the given user.
    /// </summary>
    public async Task<Folder?> GetFolderAsync(int folderId, string loginId)
    {
        return await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.LoginId == loginId);
    }

    /// <summary>
    /// Builds the breadcrumb trail from root to the given folder.
    /// </summary>
    public async Task<List<Folder>> GetBreadcrumbAsync(int? folderId, string loginId)
    {
        var breadcrumb = new List<Folder>();
        if (!folderId.HasValue) return breadcrumb;

        var current = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.LoginId == loginId);
        while (current != null)
        {
            breadcrumb.Insert(0, current);
            if (current.ParentFolderId.HasValue)
                current = await _context.Folders.FirstOrDefaultAsync(f => f.Id == current.ParentFolderId && f.LoginId == loginId);
            else
                current = null;
        }
        return breadcrumb;
    }

    /// <summary>
    /// Moves a document to a folder by folder ID (null = root).
    /// </summary>
    public async Task<bool> MoveDocumentToFolderByIdAsync(int documentId, string loginId, int? folderId)
    {
        if (folderId.HasValue)
        {
            var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.LoginId == loginId);
            if (folder == null)
                throw new ArgumentException("Target folder not found.");
        }

        var document = await GetDocumentAsync(documentId, loginId);
        if (document == null) return false;

        document.FolderId = folderId;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Document {DocumentId} moved to folder {FolderId} for user {UserId}", documentId, folderId?.ToString() ?? "(root)", loginId);
        return true;
    }

    /// <summary>
    /// Gets documents within a specific folder (or root if folderId is null).
    /// </summary>
    public async Task<List<Document>> GetDocumentsInFolderAsync(string loginId, int? folderId)
    {
        return await _context.Documents
            .Where(d => d.LoginId == loginId && d.FolderId == folderId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a document to the specified folder (or root if folderId is null).
    /// </summary>
    public async Task<Document> AddDocumentToFolderAsync(string loginId, IFormFile file, int? folderId)
    {
        var document = await AddDocumentAsync(loginId, file);
        if (folderId.HasValue)
        {
            var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.LoginId == loginId);
            if (folder != null)
            {
                document.FolderId = folderId;
                await _context.SaveChangesAsync();
            }
        }
        return document;
    }
}