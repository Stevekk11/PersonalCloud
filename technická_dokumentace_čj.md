# Technická dokumentace PersonalCloud
**Verze: 0.7.0**

## 1. Přehled projektu

PersonalCloud je moderní webová aplikace pro osobní cloudové úložiště dokumentů postavená na ASP.NET Core 9.0. Aplikace poskytuje bezpečné ukládání a správu dokumentů s pokročilými funkcemi jako autentizace uživatelů, správa kvót, multi-jazyčná podpora a integrace IoT senzorů.

### 1.1 Klíčové technologie
- **.NET 9.0** - Framework
- **ASP.NET Core MVC** - Architektura aplikace
- **Entity Framework Core** - ORM
- **ASP.NET Identity** - Autentizace a autorizace
- **SQLite** - Databáze
- **Syncfusion DocIO/XlsIO/Presentation** - Konverze dokumentů
- **MailKit** - Email notifikace
- **Serilog** - Logování

## 2. Architektura aplikace

### 2.1 Architektonický vzor
Aplikace využívá **MVC (Model-View-Controller)** architektonický vzor s následujícím rozdělením:

```
PersonalCloud/
├── Controllers/        # Řadiče (Controllers)
├── Models/            # Doménové modely
├── Views/             # Razor view šablony
├── Services/          # Business logika
├── Data/              # Database kontext a migrace
├── Helpers/           # Pomocné utility třídy
├── ViewComponents/    # Znovupoužitelné view komponenty
├── Areas/Identity/    # ASP.NET Identity stránky
├── Resources/         # Lokalizační zdroje
└── wwwroot/          # Statické soubory
```

### 2.2 Vrstvová architektura

#### Prezentační vrstva (Presentation Layer)
- **Controllers**: HomeController, DocumentController
- **Views**: Razor šablony (.cshtml)
- **ViewComponents**: StorageUsageViewComponent

#### Business logika (Business Logic Layer)
- **Services**: DocumentService, SensorService, PremiumCapacityService
- Zapouzdření business pravidel a validací

#### Datová vrstva (Data Access Layer)
- **ApplicationDbContext**: EF Core DbContext
- **Repository pattern**: Přístup k datům přes DbContext

## 3. Detailní popis tříd a komponent

### 3.1 Modely (Models)

#### 3.1.1 Document
```csharp
namespace PersonalCloud.Models;
```

**Účel**: Reprezentuje dokumentovou entitu v databázi.

**Vlastnosti**:
- `Id` (int, PK): Primární klíč
- `FileName` (string, max 500): Název souboru
- `ContentType` (string, max 100): MIME typ
- `FileSize` (long): Velikost souboru v bytech
- `StoragePath` (string): Cesta k souboru na disku
- `UploadedAt` (DateTime): Časová značka uploadu
- `LoginId` (string, FK): Foreign key na uživatele
- `User` (ApplicationUser): Navigační vlastnost

**Anotace**: 
- `[Key]`, `[Required]`, `[MaxLength]`, `[ForeignKey]`

#### 3.1.2 ApplicationUser
```csharp
namespace PersonalCloud.Models;
```

**Účel**: Rozšiřuje standardního IdentityUser o vlastní vlastnosti.

**Dědičnost**: `IdentityUser`

**Vlastní vlastnosti**:
- `IsPremium` (bool): Příznak premium účtu
- `LastLoginTime` (DateTime?): Čas posledního přihlášení

#### 3.1.3 DocumentViewModel
Pomocný view model pro předávání kolekcí dokumentů do views.

#### 3.1.4 DocumentWithSignature
Kombinuje Document s informací o podpisu PDF.

#### 3.1.5 HomeDashboardViewModel
Model pro domovskou stránku obsahující:
- Uživatelské informace
- Data ze senzorů (teplota, vlhkost)
- Nejnovější dokumenty
- Server čas

#### 3.1.6 ErrorViewModel
Model pro zobrazení chybových stránek.

### 3.2 Služby (Services)

#### 3.2.1 DocumentService
```csharp
namespace PersonalCloud.Services;
```

**Účel**: Centrální služba pro správu dokumentů.

**Konstanty**:
- `MaxStoragePerUser`: 10 GB (běžný uživatel)
- `MaxStoragePerPremiumUser`: 50 GB (premium uživatel)

**Klíčové metody**:

##### GetUserStorageUsedAsync(string loginId)
- **Popis**: Vypočítá celkové využití úložiště pro uživatele
- **Parametry**: loginId - ID uživatele
- **Návratová hodnota**: Task<long> - počet použitých bytů

##### AddDocumentAsync(string loginId, IFormFile file)
- **Popis**: Přidá nový dokument do systému
- **Logika**:
  1. Ověření zakázaných přípon (.cs, .exe, .cshtml, .js)
  2. Kontrola úložné kvóty
  3. Generování unikátního názvu souboru (GUID)
  4. Uložení na disk do složky `UserDocs/`
  5. Vytvoření záznamu v databázi
- **Exception handling**: 
  - ArgumentException: Zakázaný typ souboru
  - InvalidOperationException: Překročená kvóta

##### GetUserDocumentsAsync(string loginId)
- **Popis**: Získá seznam dokumentů uživatele
- **Řazení**: Sestupně podle UploadedAt

##### DeleteDocumentAsync(int documentId, string loginId)
- **Popis**: Smaže dokument z DB i disku
- **Bezpečnost**: 
  - Ověření vlastnictví
  - Path traversal ochrana
  - Validace, že cesta je v rámci storage root

##### GetDocumentAsync(int documentId, string loginId)
- **Popis**: Získá konkrétní dokument
- **Bezpečnost**: Ověření vlastnictví

##### GetLatestUserDocumentAsync(string loginId)
- **Popis**: Získá nejnovější dokument uživatele

#### 3.2.2 SensorService
```csharp
namespace PersonalCloud.Services;
```

**Účel**: Komunikace s IoT teploměrem/vlhkoměrem přes TCP/IP.

**Konfigurace**: 
- URL serveru: `appsettings.json -> SensorServer:Url`
- Default: `http://192.168.1.90:5000`

**Klíčové metody**:

##### GetLatestReadingAsync()
- **Popis**: Načte aktuální teplotu a vlhkost ze senzoru
- **Protokol**: TCP spojení, čtení textového řádku
- **Parsování**: Regex extrakce hodnot z formátu:
  ```
  Reading #N: Temp=XX.X , Humidity=XX.X%
  ```
- **Timeout**: 2 sekundy na připojení
- **Error handling**: 
  - Při chybě se senzor deaktivuje (`_isSensorDisabled = true`)
  - Další volání vrací (null, null) bez pokusu o připojení

**Regex vzory**:
- `TempRegex`: `Temp=([\d.]+)`
- `HumRegex`: `Humidity=([\d.]+)`

#### 3.2.3 PremiumCapacityService
```csharp
namespace PersonalCloud.Services;
```

**Účel**: Správa kapacity premium účtů na základě volného místa na disku.

**Rozhraní**: Implementuje `IPremiumCapacityService`

**Konstanta**:
- `GigabytesPerPremiumUser`: 50 GB na premium uživatele

**Klíčové metody**:

##### GetMaxPremiumUsers()
- **Popis**: Vypočítá maximální počet premium uživatelů
- **Logika**: volné místo / 50 GB (zaokrouhleno dolů)

##### GetCurrentPremiumUserCountAsync()
- **Popis**: Spočítá aktuální počet premium uživatelů v databázi

##### CanAddPremiumUserAsync()
- **Popis**: Ověří, zda lze přidat dalšího premium uživatele
- **Podmínka**: current < max

##### GetAvailableDiskSpaceGB()
- **Popis**: Získá volné místo na disku v GB
- **Implementace**: Využívá DriveInfo API

### 3.3 Řadiče (Controllers)

#### 3.3.1 HomeController
```csharp
namespace PersonalCloud.Controllers;
```

**Účel**: Řadič pro domovskou stránku a obecné akce.

**Závislosti**:
- ILogger<HomeController>
- UserManager<ApplicationUser>
- DocumentService
- SensorService

**Akce**:

##### Index()
- **Route**: `/` nebo `/Home/Index`
- **Popis**: Zobrazí dashboard s informacemi o uživateli a senzorových datech
- **View Model**: HomeDashboardViewModel
- **Data**: 
  - Server čas (UTC)
  - Teplota a vlhkost ze senzoru
  - Uživatelské info (jméno, premium status, poslední přihlášení)
  - Nejnovější dokument

##### Privacy()
- **Route**: `/Home/Privacy`
- **Popis**: Zobrazí stránku s informacemi o ochraně soukromí

##### PidBoard()
- **Route**: `/Home/PidBoard`
- **Popis**: Zobrazí IoT dashboard

##### GetSensorData() [HttpGet]
- **Route**: `/Home/GetSensorData`
- **Popis**: API endpoint vracející aktuální data ze senzoru
- **Návrat**: JSON s `{ temperature, humidity }`
- **Použití**: AJAX volání z frontendu

##### Error()
- **Popis**: Zobrazí chybovou stránku
- **Features**: 
  - Response cache disabled
  - Logování Request ID

#### 3.3.2 DocumentController
```csharp
namespace PersonalCloud.Controllers;
```

**Účel**: Řadič pro správu dokumentů.

**Autorizace**: `[Authorize]` - všechny akce vyžadují přihlášení

**Závislosti**:
- ApplicationDbContext
- DocumentService
- UserManager<ApplicationUser>
- ILogger<DocumentController>

**Pomocné metody**:

##### GetCurrentUserId()
- **Popis**: Získá ID aktuálně přihlášeného uživatele
- **Návrat**: string (User.Id)
- **Exception**: Pokud uživatel není nalezen

**Akce**:

##### Index()
- **Route**: `/Document` nebo `/Document/Index`
- **Popis**: Zobrazí seznam všech dokumentů uživatele
- **View Model**: DocumentViewModel s kolekcí DocumentWithSignature

##### UploadFile(IFormFile file) [HttpPost]
- **Route**: `/Document/UploadFile`
- **Popis**: Nahraje nový soubor
- **Validace**: 
  - Anti-forgery token
  - Soubor nesmí být null nebo prázdný
- **Zpracování chyb**:
  - ArgumentException → zakázaný typ souboru
  - InvalidOperationException → překročená kvóta
  - Obecné exception → neočekávaná chyba
- **Feedback**: Chyby uloženy do TempData["UploadError"]
- **Redirect**: Na Index po úspěchu/chybě

##### Download(int id)
- **Route**: `/Document/Download/{id}`
- **Popis**: Stáhne dokument
- **Bezpečnost**:
  - Ověření vlastnictví dokumentu
  - Kontrola existence souboru
- **Návrat**: PhysicalFile s původním názvem
- **HTTP kódy**:
  - 200: Úspěch
  - 404: Dokument nenalezen
  - 403: Nedostatečná oprávnění
  - 500: Neočekávaná chyba

##### Gallery()
- **Route**: `/Document/Gallery`
- **Popis**: Zobrazí galerii obrázků
- **Filtr**: Pouze image/* content types
- **Podporované typy**: jpeg, jpg, png, gif, webp, bmp

##### Music()
- **Route**: `/Document/Music`
- **Popis**: Zobrazí hudební knihovnu
- **Filtr**: Pouze audio/* content types
- **Podporované typy**: mpeg, mp3, wav, wma, ogg

##### GetImageDetails(int id) [HttpGet]
- **Route**: `/Document/GetImageDetails/{id}`
- **Popis**: Vrací detaily o obrázku
- **Návrat**: JSON s:
  - id, fileName, fileSize (formátovaná)
  - uploadedAt, contentType, downloadUrl
  - width, height (ze System.Drawing.Image)

##### Preview(int id) [HttpGet]
- **Route**: `/Document/Preview/{id}`
- **Popis**: Zobrazí náhled dokumentu
- **Speciální zpracování**:
  - **Word dokumenty** (.docx, .doc): Konverze na PDF pomocí Syncfusion DocIO
  - **Excel soubory** (.xlsx, .xls): Konverze na PDF pomocí Syncfusion XlsIO
  - **PowerPoint prezentace** (.pptx, .ppt): Konverze na PDF pomocí Syncfusion Presentation
  - **Ostatní**: Přímé zobrazení s původním MIME typem
- **Technologie**: Syncfusion DocIORenderer, XlsIORenderer, PresentationRenderer
- **Návrat**: 
  - PDF stream pro Office dokumenty
  - PhysicalFile pro ostatní typy
  - enableRangeProcessing: true (pro video/audio streaming)

##### Delete(int id) [HttpPost]
- **Route**: `/Document/Delete/{id}`
- **Popis**: Smaže dokument
- **Validace**: Anti-forgery token
- **Redirect**: Na Index po smazání

**Pomocné metody**:

##### FormatFileSize(long bytes)
- **Popis**: Formátuje velikost souboru (B, KB, MB, GB)
- **Implementace**: Dělení 1024 s apropriátním suffixem

### 3.4 Datová vrstva (Data)

#### 3.4.1 ApplicationDbContext
```csharp
namespace PersonalCloud.Data;
```

**Účel**: Entity Framework Core databázový kontext.

**Dědičnost**: `IdentityDbContext<ApplicationUser>`

**DbSets**:
- `Documents` - kolekce dokumentů

**OnModelCreating**:
- Vytvoření indexu na `Document.FileName` (IX_FileName)

**Migrace**:
- Složka: `Data/Migrations/`
- Automatická migrace: Zakomentována v Program.cs
- Ruční migrace: `dotnet ef database update`

### 3.5 Pomocné třídy (Helpers)

#### 3.5.1 SmtpEmailSender
```csharp
namespace PersonalCloud.Helpers;
```

**Účel**: Odesílání emailů přes SMTP protokol.

**Rozhraní**: Implementuje `IEmailSender` (ASP.NET Identity)

**Závislosti**:
- IConfiguration (pro SMTP nastavení)
- ILogger<SmtpEmailSender>
- MailKit.Net.Smtp

**Konfigurace** (appsettings.json):
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

**Klíčová metoda**:

##### SendEmailAsync(string email, string subject, string htmlMessage)
- **Popis**: Asynchronně odešle HTML email
- **Protokol**: SMTP s autentizací
- **Message template**:
  - From: "Cloudové úložiště - potvrzení emailu"
  - Body: HTML s vlastní stopkou (pozdrav od Stevek)
- **Error handling**: 
  - Logování varování při selhání
  - Nepadá aplikaci (graceful degradation)

### 3.6 View komponenty (ViewComponents)

#### 3.6.1 StorageUsageViewComponent
```csharp
namespace PersonalCloud.ViewComponents;
```

**Účel**: Zobrazení progress baru s využitím úložiště.

**Závislosti**:
- DocumentService
- UserManager<ApplicationUser>
- ILogger

**View Model**: StorageUsageViewModel
- `UsedBytes` (long)
- `MaxBytes` (long)
- `PercentageUsed` (double)
- `UsedFormatted` (string)
- `MaxFormatted` (string)

**Logika**:
1. Kontrola, zda je uživatel přihlášen
2. Získání ID uživatele
3. Výpočet použitého místa
4. Určení kvóty (10 GB vs 50 GB podle IsPremium)
5. Výpočet procent
6. Formátování velikostí

**Použití v view**:
```csharp
@await Component.InvokeAsync("StorageUsage")
```

## 4. Bezpečnostní mechanismy

### 4.1 Autentizace a autorizace
- **ASP.NET Identity**: Správa uživatelů a přihlašování
- **[Authorize] atribut**: Ochrana controller akcí
- **RequireConfirmedAccount**: false (lze aktivovat pro email confirmation)

### 4.2 Ochrana souborů

#### 4.2.1 Blokované příponyz (DocumentService)
```csharp
var disallowedExtensions = new[] { ".cs", ".exe", ".cshtml", ".js" };
```

#### 4.2.2 Path Traversal ochrana
- Validace cest v DeleteDocumentAsync
- Kontrola, že cesta je v rámci storage root
- Použití Path.GetFullPath pro normalizaci

#### 4.2.3 Content-Type manipulace
V Program.cs při servírování statických souborů:
```csharp
if (extensions.Contains(Path.GetExtension(ctx.File.PhysicalPath).ToLower()))
{
    ctx.Context.Response.ContentType = "application/octet-stream";
}
```
Zamezení exekuce nebezpečných souborů.

### 4.3 Upload limity
- **Velikost souboru**: Max 5 GB
- **Kestrel konfigurace**: `MaxRequestBodySize = 5GB`
- **FormOptions**: `MultipartBodyLengthLimit = 5GB`

### 4.4 Anti-CSRF
- `[ValidateAntiForgeryToken]` na POST akcích
- Ochrana před Cross-Site Request Forgery

### 4.5 HTTPS
- Automatické přesměrování na HTTPS (Program.cs)
- HSTS (HTTP Strict Transport Security) v produkci

### 4.6 Database Security
- Parametrizované dotazy (EF Core)
- Ochrana před SQL injection

## 5. Konfigurace a nastavení

### 5.1 appsettings.json struktura

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
  "SYNCFUSION_LICENSE_KEY": "klíč"
}
```

### 5.2 Environment specifické konfigurace
- `appsettings.Development.json` - pro vývoj
- `appsettings.Production.json` - pro produkci (nedodán)

### 5.3 User Secrets
- User Secrets ID: `aspnet-PersonalCloud-626261b8-e025-450f-b8e5-3e2e93e78eef`
- Pro bezpečné ukládání hesel mimo source control

## 6. Databázové schéma

### 6.1 Tabulky

#### AspNetUsers (ASP.NET Identity)
Standardní Identity tabulka s rozšířením:
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

**Indexy**:
- IX_FileName (FileName)
- FK_Documents_AspNetUsers (LoginId)

#### AspNetRoles, AspNetUserRoles, AspNetUserClaims, atd.
Standardní Identity tabulky pro role, claims, tokens, logins.

### 6.2 Entity Relationships
```
AspNetUsers (1) ──< (∞) Documents
```
- One-to-Many vztah
- Kaskádové mazání není explicitně definováno

## 7. Lokalizace (Internationalization)

### 7.1 Podporované jazyky
- **cs** (Czech) - výchozí
- **en** (English)

### 7.2 Konfigurace
V Program.cs:
```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "en", "cs" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("cs")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
```

### 7.3 Přepínání jazyka
- Query string parametr: `?culture=en` nebo `?culture=cs`
- Využívá QueryStringRequestCultureProvider

### 7.4 Resource soubory
Umístění: `Resources/`
- Controllers.HomeController.cs.resx
- Controllers.HomeController.en.resx
- atd.

## 8. Logování (Logging)

### 8.1 Serilog konfigurace
- **Minimum level**: Information
- **Sinks**: 
  1. Console
  2. File (rolling daily)

### 8.2 Log soubory
- Umístění: `Logs/PersonalCloud-YYYYMMDD.log`
- Rotace: Denní
- Formát: Strukturované logy

### 8.3 Log kategorie
- Information: Běžné operace
- Warning: Nestandartní situace (chybějící senzor, zakázaný upload)
- Error: Neočekávané chyby
- Debug: Detailní debug informace

### 8.4 Příklady logů
```
[INF] User user123 is viewing their documents.
[WRN] File upload blocked: disallowed extension .exe for user user123
[ERR] Unexpected error while downloading document 42
```

## 9. IoT integrace

### 9.1 Senzorový protokol
- **Protokol**: TCP/IP
- **Port**: 5000 (výchozí)
- **Formát odpovědi**:
  ```
  Reading #123: Temp=23.5 , Humidity=45.2%
  ```

### 9.2 Hardware
- **Platforma**: MicroPython
- **Senzory**: DHT22 nebo podobný pro teplotu a vlhkost
- **Komunikace**: TCP server na ESP32/ESP8266

### 9.3 Chování při nedostupnosti
- Timeout 2 sekundy
- Při chybě se senzor deaktivuje
- Další volání vrací (null, null) bez pokusu o spojení
- Reaktivace pouze restartem aplikace

## 10. Konverze dokumentů (Syncfusion)

### 10.1 Podporované konverze
Všechny následující formáty se konvertují na PDF pro preview:

1. **Word dokumenty**:
   - .docx (application/vnd.openxmlformats-officedocument.wordprocessingml.document)
   - .doc (application/msword)
   - **Engine**: Syncfusion DocIO + DocIORenderer

2. **Excel soubory**:
   - .xlsx (application/vnd.openxmlformats-officedocument.spreadsheetml.sheet)
   - .xls (application/vnd.ms-excel)
   - **Engine**: Syncfusion XlsIO + XlsIORenderer

3. **PowerPoint prezentace**:
   - .pptx (application/vnd.openxmlformats-officedocument.presentationml.presentation)
   - .ppt (application/vnd.ms-powerpoint)
   - **Engine**: Syncfusion Presentation + PresentationRenderer

### 10.2 Proces konverze
1. Načtení souboru z úložiště
2. Otevření pomocí příslušného Syncfusion enginu
3. Konverze na PDF v paměti (MemoryStream)
4. Vrácení PDF streamu klientovi

### 10.3 Licencování
- Vyžaduje Syncfusion licenci
- Registrace v Program.cs: `SyncfusionLicenseProvider.RegisterLicense(key)`
- Klíč uložen v konfiguraci (`SYNCFUSION_LICENSE_KEY`)

## 11. Deployment

### 11.1 Publikace
```bash
dotnet publish -c Release -o ./publish
```

**Vlastnosti publikace** (z .csproj):
- `PublishSingleFile`: true
- `SelfContained`: true
- `EnableCompressionInSingleFile`: true
- `IncludeAllContentForSelfExtract`: false

**Výsledek**: Jediný spustitelný soubor s veškerými závislostmi.

### 11.2 Požadavky na server
- **.NET 9.0 Runtime** (pokud SelfContained = false)
- **Operační systém**: Linux, Windows, macOS
- **Diskový prostor**: Závisí na uživatelských datech (min. několik GB)
- **Oprávnění**: 
  - Read/Write na `app.db`
  - Read/Write na `UserDocs/`
  - Read/Write na `Logs/`

### 11.3 Reverse proxy
Doporučená konfigurace s Nginx nebo IIS:
- Proxy na Kestrel (výchozí port 5000/5001)
- SSL/TLS terminace na reverse proxy
- Static file caching
- Request size limit (5 GB)

### 11.4 Produkční checklist
- [ ] Nastavit `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Nakonfigurovat appsettings.Production.json
- [ ] Použít skutečnou databázi (nebo zabezpečit SQLite)
- [ ] Nastavit SMTP pro emaily
- [ ] Konfigurace SSL certifikátů
- [ ] Pravidelné zálohy databáze a UserDocs/
- [ ] Monitoring a alerting
- [ ] Rate limiting pro uploady

## 12. Testování

### 12.1 Testovací infrastruktura
V současné verzi nejsou unit testy implementovány.

### 12.2 Manuální testování
- Registrace/přihlášení uživatelů
- Upload různých typů souborů
- Download a preview dokumentů
- Správa kvót
- Galerie a hudební knihovna
- IoT senzor integrace

## 13. Výkonnostní charakteristiky

### 13.1 Limity
- **Max velikost souboru**: 5 GB
- **Max storage (běžný)**: 10 GB
- **Max storage (premium)**: 50 GB
- **Současné uploady**: Omezeno pouze Kestrel konfigurací

### 13.2 Optimalizace
- **Entity Framework**: 
  - Asynchronní operace
  - Indexy na často vyhledávané sloupce
- **File streaming**: 
  - PhysicalFile pro přímé streamování
  - enableRangeProcessing pro video/audio
- **Caching**: 
  - Response caching na Error akcích
  - Static file caching
- **Database**: 
  - SQLite s Cache=Shared

## 14. Známá omezení a budoucí vylepšení

### 14.1 Aktuální omezení
- Žádné unit testy
- Kaskádové mazání není definováno
- Senzor se neobnovuje po chybě bez restartu
- Syncfusion trial/placená licence
- Premium status lze nastavit pouze ručně v DB

### 14.2 Možná vylepšení
- Implementace role systému (Admin, User, Premium)
- Sdílení souborů mezi uživateli
- Verzování dokumentů
- Fulltextové vyhledávání
- Thumbnail generování pro obrázky
- Zip download pro multiple files
- Trash/Recycle bin
- API pro mobilní aplikace
- Docker containerizace

## 15. Struktura view šablon

### 15.1 Layout
- `Views/Shared/_Layout.cshtml` - Hlavní layout
- `Views/Shared/_LoginPartial.cshtml` - Login/Logout odkazy
- Navigation bar s odkazy na Home, Documents, Gallery, Music

### 15.2 Home views
- `Views/Home/Index.cshtml` - Dashboard
- `Views/Home/Privacy.cshtml` - Privacy policy
- `Views/Home/PidBoard.cshtml` - IoT dashboard

### 15.3 Document views
- `Views/Document/Index.cshtml` - Seznam dokumentů
- `Views/Document/Gallery.cshtml` - Galerie obrázků
- `Views/Document/Music.cshtml` - Hudební knihovna

### 15.4 Identity views
- `Areas/Identity/Pages/Account/Login.cshtml` - Přihlášení
- `Areas/Identity/Pages/Account/Register.cshtml` - Registrace
- `Areas/Identity/Pages/Account/Manage/` - Správa účtu

## 16. Závěr

PersonalCloud je kompletní řešení pro osobní cloudové úložiště s důrazem na:
- **Bezpečnost**: Validace souborů, path traversal ochrana, HTTPS
- **Výkon**: Asynchronní operace, streaming velkých souborů
- **Rozšiřitelnost**: Modulární architektura, DI kontejner
- **Uživatelské prostředí**: Multi-jazyk, responsive design
- **Moderní technologie**: .NET 9.0, Entity Framework Core, Syncfusion

Aplikace je vhodná jak pro osobní použití, tak jako základ pro komerční cloudové řešení s dalšími úpravami.

---
**Poslední aktualizace**: 2026-02-10  
**Autor**: Stevekk11  
**Verze dokumentace**: 1.0
