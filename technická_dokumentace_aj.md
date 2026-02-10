# PersonalCloud Technical Documentation
**Version: 0.7.0**

## 1. Project Overview

PersonalCloud is a modern web application for personal cloud document storage built on ASP.NET Core 9.0. The application provides secure document storage and management with advanced features such as user authentication, quota management, multi-language support, and IoT sensor integration.

### 1.1 Key Technologies
- **.NET 9.0** - Framework
- **ASP.NET Core MVC** - Application architecture
- **Entity Framework Core** - ORM
- **ASP.NET Identity** - Authentication and authorization
- **SQLite** - Database
- **Syncfusion DocIO/XlsIO/Presentation** - Document conversion
- **MailKit** - Email notifications
- **Serilog** - Logging

## 2. Application Architecture

### 2.1 Architectural Pattern
The application uses the **MVC (Model-View-Controller)** architectural pattern with the following structure:

```
PersonalCloud/
├── Controllers/        # Controllers
├── Models/            # Domain models
├── Views/             # Razor view templates
├── Services/          # Business logic
├── Data/              # Database context and migrations
├── Helpers/           # Utility helper classes
├── ViewComponents/    # Reusable view components
├── Areas/Identity/    # ASP.NET Identity pages
├── Resources/         # Localization resources
└── wwwroot/          # Static files
```

### 2.2 Layered Architecture

#### Presentation Layer
- **Controllers**: HomeController, DocumentController
- **Views**: Razor templates (.cshtml)
- **ViewComponents**: StorageUsageViewComponent

#### Business Logic Layer
- **Services**: DocumentService, SensorService, PremiumCapacityService
- Encapsulates business rules and validations

#### Data Access Layer
- **ApplicationDbContext**: EF Core DbContext
- **Repository pattern**: Data access through DbContext

## 3. Detailed Class and Component Description

### 3.1 Models

#### 3.1.1 Document
```csharp
namespace PersonalCloud.Models;
```

**Purpose**: Represents a document entity in the database.

**Properties**:
- `Id` (int, PK): Primary key
- `FileName` (string, max 500): File name
- `ContentType` (string, max 100): MIME type
- `FileSize` (long): File size in bytes
- `StoragePath` (string): File path on disk
- `UploadedAt` (DateTime): Upload timestamp
- `LoginId` (string, FK): Foreign key to user
- `User` (ApplicationUser): Navigation property

**Annotations**: 
- `[Key]`, `[Required]`, `[MaxLength]`, `[ForeignKey]`

#### 3.1.2 ApplicationUser
```csharp
namespace PersonalCloud.Models;
```

**Purpose**: Extends the standard IdentityUser with custom properties.

**Inheritance**: `IdentityUser`

**Custom Properties**:
- `IsPremium` (bool): Premium account flag
- `LastLoginTime` (DateTime?): Last login time

#### 3.1.3 DocumentViewModel
Helper view model for passing document collections to views.

#### 3.1.4 DocumentWithSignature
Combines Document with PDF signature information.

#### 3.1.5 HomeDashboardViewModel
Model for the home page containing:
- User information
- Sensor data (temperature, humidity)
- Latest documents
- Server time

#### 3.1.6 ErrorViewModel
Model for displaying error pages.

### 3.2 Services

#### 3.2.1 DocumentService
```csharp
namespace PersonalCloud.Services;
```

**Purpose**: Central service for document management.

**Constants**:
- `MaxStoragePerUser`: 10 GB (regular user)
- `MaxStoragePerPremiumUser`: 50 GB (premium user)

**Key Methods**:

##### GetUserStorageUsedAsync(string loginId)
- **Description**: Calculates total storage usage for a user
- **Parameters**: loginId - User ID
- **Return Value**: Task<long> - number of bytes used

##### AddDocumentAsync(string loginId, IFormFile file)
- **Description**: Adds a new document to the system
- **Logic**:
  1. Validate disallowed extensions (.cs, .exe, .cshtml, .js)
  2. Check storage quota
  3. Generate unique file name (GUID)
  4. Save to disk in `UserDocs/` folder
  5. Create database record
- **Exception Handling**: 
  - ArgumentException: Disallowed file type
  - InvalidOperationException: Quota exceeded

##### GetUserDocumentsAsync(string loginId)
- **Description**: Retrieves user's document list
- **Sorting**: Descending by UploadedAt

##### DeleteDocumentAsync(int documentId, string loginId)
- **Description**: Deletes document from DB and disk
- **Security**: 
  - Ownership verification
  - Path traversal protection
  - Validates path is within storage root

##### GetDocumentAsync(int documentId, string loginId)
- **Description**: Gets a specific document
- **Security**: Ownership verification

##### GetLatestUserDocumentAsync(string loginId)
- **Description**: Gets user's latest document

#### 3.2.2 SensorService
```csharp
namespace PersonalCloud.Services;
```

**Purpose**: Communication with IoT temperature/humidity sensor via TCP/IP.

**Configuration**: 
- Server URL: `appsettings.json -> SensorServer:Url`
- Default: `http://192.168.1.90:5000`

**Key Methods**:

##### GetLatestReadingAsync()
- **Description**: Reads current temperature and humidity from sensor
- **Protocol**: TCP connection, reading text line
- **Parsing**: Regex extraction from format:
  ```
  Reading #N: Temp=XX.X , Humidity=XX.X%
  ```
- **Timeout**: 2 seconds for connection
- **Error Handling**: 
  - On error, sensor is disabled (`_isSensorDisabled = true`)
  - Subsequent calls return (null, null) without connection attempts

**Regex Patterns**:
- `TempRegex`: `Temp=([\d.]+)`
- `HumRegex`: `Humidity=([\d.]+)`

#### 3.2.3 PremiumCapacityService
```csharp
namespace PersonalCloud.Services;
```

**Purpose**: Manages premium account capacity based on available disk space.

**Interface**: Implements `IPremiumCapacityService`

**Constant**:
- `GigabytesPerPremiumUser`: 50 GB per premium user

**Key Methods**:

##### GetMaxPremiumUsers()
- **Description**: Calculates maximum number of premium users
- **Logic**: free space / 50 GB (rounded down)

##### GetCurrentPremiumUserCountAsync()
- **Description**: Counts current premium users in database

##### CanAddPremiumUserAsync()
- **Description**: Checks if another premium user can be added
- **Condition**: current < max

##### GetAvailableDiskSpaceGB()
- **Description**: Gets available disk space in GB
- **Implementation**: Uses DriveInfo API

### 3.3 Controllers

#### 3.3.1 HomeController
```csharp
namespace PersonalCloud.Controllers;
```

**Purpose**: Controller for home page and general actions.

**Dependencies**:
- ILogger<HomeController>
- UserManager<ApplicationUser>
- DocumentService
- SensorService

**Actions**:

##### Index()
- **Route**: `/` or `/Home/Index`
- **Description**: Displays dashboard with user info and sensor data
- **View Model**: HomeDashboardViewModel
- **Data**: 
  - Server time (UTC)
  - Temperature and humidity from sensor
  - User info (name, premium status, last login)
  - Latest document

##### Privacy()
- **Route**: `/Home/Privacy`
- **Description**: Displays privacy policy page

##### PidBoard()
- **Route**: `/Home/PidBoard`
- **Description**: Displays IoT dashboard

##### GetSensorData() [HttpGet]
- **Route**: `/Home/GetSensorData`
- **Description**: API endpoint returning current sensor data
- **Return**: JSON with `{ temperature, humidity }`
- **Usage**: AJAX calls from frontend

##### Error()
- **Description**: Displays error page
- **Features**: 
  - Response cache disabled
  - Request ID logging

#### 3.3.2 DocumentController
```csharp
namespace PersonalCloud.Controllers;
```

**Purpose**: Controller for document management.

**Authorization**: `[Authorize]` - all actions require authentication

**Dependencies**:
- ApplicationDbContext
- DocumentService
- UserManager<ApplicationUser>
- ILogger<DocumentController>

**Helper Methods**:

##### GetCurrentUserId()
- **Description**: Gets currently logged-in user's ID
- **Return**: string (User.Id)
- **Exception**: If user not found

**Actions**:

##### Index()
- **Route**: `/Document` or `/Document/Index`
- **Description**: Displays list of all user documents
- **View Model**: DocumentViewModel with DocumentWithSignature collection

##### UploadFile(IFormFile file) [HttpPost]
- **Route**: `/Document/UploadFile`
- **Description**: Uploads a new file
- **Validation**: 
  - Anti-forgery token
  - File must not be null or empty
- **Error Handling**:
  - ArgumentException → disallowed file type
  - InvalidOperationException → quota exceeded
  - General exception → unexpected error
- **Feedback**: Errors stored in TempData["UploadError"]
- **Redirect**: To Index after success/error

##### Download(int id)
- **Route**: `/Document/Download/{id}`
- **Description**: Downloads document
- **Security**:
  - Document ownership verification
  - File existence check
- **Return**: PhysicalFile with original name
- **HTTP Codes**:
  - 200: Success
  - 404: Document not found
  - 403: Insufficient permissions
  - 500: Unexpected error

##### Gallery()
- **Route**: `/Document/Gallery`
- **Description**: Displays image gallery
- **Filter**: Only image/* content types
- **Supported Types**: jpeg, jpg, png, gif, webp, bmp

##### Music()
- **Route**: `/Document/Music`
- **Description**: Displays music library
- **Filter**: Only audio/* content types
- **Supported Types**: mpeg, mp3, wav, wma, ogg

##### GetImageDetails(int id) [HttpGet]
- **Route**: `/Document/GetImageDetails/{id}`
- **Description**: Returns image details
- **Return**: JSON with:
  - id, fileName, fileSize (formatted)
  - uploadedAt, contentType, downloadUrl
  - width, height (from System.Drawing.Image)

##### Preview(int id) [HttpGet]
- **Route**: `/Document/Preview/{id}`
- **Description**: Displays document preview
- **Special Processing**:
  - **Word documents** (.docx, .doc): Convert to PDF using Syncfusion DocIO
  - **Excel files** (.xlsx, .xls): Convert to PDF using Syncfusion XlsIO
  - **PowerPoint presentations** (.pptx, .ppt): Convert to PDF using Syncfusion Presentation
  - **Others**: Direct display with original MIME type
- **Technologies**: Syncfusion DocIORenderer, XlsIORenderer, PresentationRenderer
- **Return**: 
  - PDF stream for Office documents
  - PhysicalFile for other types
  - enableRangeProcessing: true (for video/audio streaming)

##### Delete(int id) [HttpPost]
- **Route**: `/Document/Delete/{id}`
- **Description**: Deletes document
- **Validation**: Anti-forgery token
- **Redirect**: To Index after deletion

**Helper Methods**:

##### FormatFileSize(long bytes)
- **Description**: Formats file size (B, KB, MB, GB)
- **Implementation**: Division by 1024 with appropriate suffix

### 3.4 Data Layer

#### 3.4.1 ApplicationDbContext
```csharp
namespace PersonalCloud.Data;
```

**Purpose**: Entity Framework Core database context.

**Inheritance**: `IdentityDbContext<ApplicationUser>`

**DbSets**:
- `Documents` - document collection

**OnModelCreating**:
- Creates index on `Document.FileName` (IX_FileName)

**Migrations**:
- Folder: `Data/Migrations/`
- Auto-migration: Commented out in Program.cs
- Manual migration: `dotnet ef database update`

### 3.5 Helper Classes

#### 3.5.1 SmtpEmailSender
```csharp
namespace PersonalCloud.Helpers;
```

**Purpose**: Sending emails via SMTP protocol.

**Interface**: Implements `IEmailSender` (ASP.NET Identity)

**Dependencies**:
- IConfiguration (for SMTP settings)
- ILogger<SmtpEmailSender>
- MailKit.Net.Smtp

**Configuration** (appsettings.json):
```json
"Email": {
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "Username": "email@example.com",
    "Password": "app-password"
  },
  "From": "email@example.com"
}
```

**Key Method**:

##### SendEmailAsync(string email, string subject, string htmlMessage)
- **Description**: Asynchronously sends HTML email
- **Protocol**: SMTP with authentication
- **Message Template**:
  - From: "Cloudové úložiště - potvrzení emailu"
  - Body: HTML with custom footer (greetings from Stevek)
- **Error Handling**: 
  - Warning logged on failure
  - Doesn't crash application (graceful degradation)

### 3.6 View Components

#### 3.6.1 StorageUsageViewComponent
```csharp
namespace PersonalCloud.ViewComponents;
```

**Purpose**: Displays storage usage progress bar.

**Dependencies**:
- DocumentService
- UserManager<ApplicationUser>
- ILogger

**View Model**: StorageUsageViewModel
- `UsedBytes` (long)
- `MaxBytes` (long)
- `PercentageUsed` (double)
- `UsedFormatted` (string)
- `MaxFormatted` (string)

**Logic**:
1. Check if user is authenticated
2. Get user ID
3. Calculate used space
4. Determine quota (10 GB vs 50 GB based on IsPremium)
5. Calculate percentage
6. Format sizes

**Usage in Views**:
```csharp
@await Component.InvokeAsync("StorageUsage")
```

## 4. Security Mechanisms

### 4.1 Authentication and Authorization
- **ASP.NET Identity**: User management and login
- **[Authorize] attribute**: Controller action protection
- **RequireConfirmedAccount**: false (can be activated for email confirmation)

### 4.2 File Protection

#### 4.2.1 Blocked Extensions (DocumentService)
```csharp
var disallowedExtensions = new[] { ".cs", ".exe", ".cshtml", ".js" };
```

#### 4.2.2 Path Traversal Protection
- Path validation in DeleteDocumentAsync
- Check that path is within storage root
- Use Path.GetFullPath for normalization

#### 4.2.3 Content-Type Manipulation
In Program.cs when serving static files:
```csharp
if (extensions.Contains(Path.GetExtension(ctx.File.PhysicalPath).ToLower()))
{
    ctx.Context.Response.ContentType = "application/octet-stream";
}
```
Prevents execution of dangerous files.

### 4.3 Upload Limits
- **File Size**: Max 5 GB
- **Kestrel Configuration**: `MaxRequestBodySize = 5GB`
- **FormOptions**: `MultipartBodyLengthLimit = 5GB`

### 4.4 Anti-CSRF
- `[ValidateAntiForgeryToken]` on POST actions
- Protection against Cross-Site Request Forgery

### 4.5 HTTPS
- Automatic HTTPS redirection (Program.cs)
- HSTS (HTTP Strict Transport Security) in production

### 4.6 Database Security
- Parameterized queries (EF Core)
- SQL injection protection

## 5. Configuration and Settings

### 5.1 appsettings.json Structure

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "DataSource=app.db;Cache=Shared"
  },
  "Storage": {
    "Root": "UserDocs"
  },
  "Email": {
    "Smtp": {
      "Host": "smtp.gmail.com",
      "Port": "587",
      "Username": "user@example.com",
      "Password": "password"
    },
    "From": "user@example.com"
  },
  "SensorServer": {
    "Url": "http://192.168.1.90:5000"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/PersonalCloud--.log",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "SYNCFUSION_LICENSE_KEY": "key"
}
```

### 5.2 Environment-Specific Configuration
- `appsettings.Development.json` - for development
- `appsettings.Production.json` - for production (not included)

### 5.3 User Secrets
- User Secrets ID: `aspnet-PersonalCloud-626261b8-e025-450f-b8e5-3e2e93e78eef`
- For secure storage of passwords outside source control

## 6. Database Schema

### 6.1 Tables

#### AspNetUsers (ASP.NET Identity)
Standard Identity table with extensions:
- Id (string, PK)
- UserName, Email, PasswordHash, etc.
- **IsPremium** (bit)
- **LastLoginTime** (datetime2)

#### Documents
- Id (int, PK, Identity)
- FileName (nvarchar(500), NOT NULL)
- ContentType (nvarchar(100), NOT NULL)
- FileSize (bigint, NOT NULL)
- StoragePath (nvarchar(max), NOT NULL)
- UploadedAt (datetime2, NOT NULL)
- LoginId (nvarchar(450), FK → AspNetUsers.Id)

**Indexes**:
- IX_FileName (FileName)
- FK_Documents_AspNetUsers (LoginId)

#### AspNetRoles, AspNetUserRoles, AspNetUserClaims, etc.
Standard Identity tables for roles, claims, tokens, logins.

### 6.2 Entity Relationships
```
AspNetUsers (1) ──< (∞) Documents
```
- One-to-Many relationship
- Cascade delete not explicitly defined

## 7. Localization (Internationalization)

### 7.1 Supported Languages
- **cs** (Czech) - default
- **en** (English)

### 7.2 Configuration
In Program.cs:
```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "en", "cs" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("cs")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
```

### 7.3 Language Switching
- Query string parameter: `?culture=en` or `?culture=cs`
- Uses QueryStringRequestCultureProvider

### 7.4 Resource Files
Location: `Resources/`
- Controllers.HomeController.cs.resx
- Controllers.HomeController.en.resx
- etc.

## 8. Logging

### 8.1 Serilog Configuration
- **Minimum Level**: Information
- **Sinks**: 
  1. Console
  2. File (rolling daily)

### 8.2 Log Files
- Location: `Logs/PersonalCloud-YYYYMMDD.log`
- Rotation: Daily
- Format: Structured logs

### 8.3 Log Categories
- Information: Normal operations
- Warning: Non-standard situations (missing sensor, blocked upload)
- Error: Unexpected errors
- Debug: Detailed debug information

### 8.4 Example Logs
```
[INF] User user123 is viewing their documents.
[WRN] File upload blocked: disallowed extension .exe for user user123
[ERR] Unexpected error while downloading document 42
```

## 9. IoT Integration

### 9.1 Sensor Protocol
- **Protocol**: TCP/IP
- **Port**: 5000 (default)
- **Response Format**:
  ```
  Reading #123: Temp=23.5 , Humidity=45.2%
  ```

### 9.2 Hardware
- **Platform**: MicroPython
- **Sensors**: DHT22 or similar for temperature and humidity
- **Communication**: TCP server on ESP32/ESP8266

### 9.3 Unavailability Behavior
- 2-second timeout
- On error, sensor is disabled
- Subsequent calls return (null, null) without connection attempts
- Reactivation only by application restart

## 10. Document Conversion (Syncfusion)

### 10.1 Supported Conversions
All following formats are converted to PDF for preview:

1. **Word Documents**:
   - .docx (application/vnd.openxmlformats-officedocument.wordprocessingml.document)
   - .doc (application/msword)
   - **Engine**: Syncfusion DocIO + DocIORenderer

2. **Excel Files**:
   - .xlsx (application/vnd.openxmlformats-officedocument.spreadsheetml.sheet)
   - .xls (application/vnd.ms-excel)
   - **Engine**: Syncfusion XlsIO + XlsIORenderer

3. **PowerPoint Presentations**:
   - .pptx (application/vnd.openxmlformats-officedocument.presentationml.presentation)
   - .ppt (application/vnd.ms-powerpoint)
   - **Engine**: Syncfusion Presentation + PresentationRenderer

### 10.2 Conversion Process
1. Load file from storage
2. Open using appropriate Syncfusion engine
3. Convert to PDF in memory (MemoryStream)
4. Return PDF stream to client

### 10.3 Licensing
- Requires Syncfusion license
- Registration in Program.cs: `SyncfusionLicenseProvider.RegisterLicense(key)`
- Key stored in configuration (`SYNCFUSION_LICENSE_KEY`)

## 11. Deployment

### 11.1 Publishing
```bash
dotnet publish -c Release -o ./publish
```

**Publishing Properties** (from .csproj):
- `PublishSingleFile`: true
- `SelfContained`: true
- `EnableCompressionInSingleFile`: true
- `IncludeAllContentForSelfExtract`: false

**Result**: Single executable file with all dependencies.

### 11.2 Server Requirements
- **.NET 9.0 Runtime** (if SelfContained = false)
- **Operating System**: Linux, Windows, macOS
- **Disk Space**: Depends on user data (minimum several GB)
- **Permissions**: 
  - Read/Write on `app.db`
  - Read/Write on `UserDocs/`
  - Read/Write on `Logs/`

### 11.3 Reverse Proxy
Recommended configuration with Nginx or IIS:
- Proxy to Kestrel (default port 5000/5001)
- SSL/TLS termination at reverse proxy
- Static file caching
- Request size limit (5 GB)

### 11.4 Production Checklist
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure appsettings.Production.json
- [ ] Use real database (or secure SQLite)
- [ ] Set up SMTP for emails
- [ ] Configure SSL certificates
- [ ] Regular backups of database and UserDocs/
- [ ] Monitoring and alerting
- [ ] Rate limiting for uploads

## 12. Testing

### 12.1 Test Infrastructure
In current version, unit tests are not implemented.

### 12.2 Manual Testing
- User registration/login
- Upload various file types
- Download and preview documents
- Quota management
- Gallery and music library
- IoT sensor integration

## 13. Performance Characteristics

### 13.1 Limits
- **Max File Size**: 5 GB
- **Max Storage (Regular)**: 10 GB
- **Max Storage (Premium)**: 50 GB
- **Concurrent Uploads**: Limited only by Kestrel configuration

### 13.2 Optimizations
- **Entity Framework**: 
  - Asynchronous operations
  - Indexes on frequently searched columns
- **File Streaming**: 
  - PhysicalFile for direct streaming
  - enableRangeProcessing for video/audio
- **Caching**: 
  - Response caching on Error actions
  - Static file caching
- **Database**: 
  - SQLite with Cache=Shared

## 14. Known Limitations and Future Improvements

### 14.1 Current Limitations
- No unit tests
- Cascade delete not defined
- Sensor doesn't recover after error without restart
- Syncfusion trial/paid license required
- Premium status can only be set manually in DB

### 14.2 Possible Improvements
- Implement role system (Admin, User, Premium)
- File sharing between users
- Document versioning
- Full-text search
- Thumbnail generation for images
- Zip download for multiple files
- Trash/Recycle bin
- API for mobile applications
- Docker containerization

## 15. View Template Structure

### 15.1 Layout
- `Views/Shared/_Layout.cshtml` - Main layout
- `Views/Shared/_LoginPartial.cshtml` - Login/Logout links
- Navigation bar with links to Home, Documents, Gallery, Music

### 15.2 Home Views
- `Views/Home/Index.cshtml` - Dashboard
- `Views/Home/Privacy.cshtml` - Privacy policy
- `Views/Home/PidBoard.cshtml` - IoT dashboard

### 15.3 Document Views
- `Views/Document/Index.cshtml` - Document list
- `Views/Document/Gallery.cshtml` - Image gallery
- `Views/Document/Music.cshtml` - Music library

### 15.4 Identity Views
- `Areas/Identity/Pages/Account/Login.cshtml` - Login
- `Areas/Identity/Pages/Account/Register.cshtml` - Registration
- `Areas/Identity/Pages/Account/Manage/` - Account management

## 16. Conclusion

PersonalCloud is a complete solution for personal cloud storage with emphasis on:
- **Security**: File validation, path traversal protection, HTTPS
- **Performance**: Asynchronous operations, large file streaming
- **Extensibility**: Modular architecture, DI container
- **User Experience**: Multi-language, responsive design
- **Modern Technologies**: .NET 9.0, Entity Framework Core, Syncfusion

The application is suitable for both personal use and as a foundation for a commercial cloud solution with further modifications.

---
**Last Updated**: 2026-02-10  
**Author**: Stevekk11  
**Documentation Version**: 1.0
