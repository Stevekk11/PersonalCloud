# PersonalCloud
# Version 1.0.0

A modern, secure ASP.NET Core 9.0 web application for personal document storage and management with user authentication, multi-language support, and IoT sensor integration.

## 📋 Table of Contents
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [Development](#development)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## ✨ Features

### Core Functionality
- **User Authentication**: Secure user registration and login with ASP.NET Identity
- **Document Management**: Upload, download, view, and delete personal documents
- **Storage Quota Management**: 10 GB for regular users, 50 GB for premium users
- **File Security**: Automatic content-type validation and protection against malicious file execution
- **Large File Support**: Upload files up to 5 GB in size

### Advanced Features
- **Multi-language Support**: Full localization support for English and Czech
- **Email Notifications**: SMTP-based email notifications via MailKit
- **IoT Sensor Integration**: Real-time temperature and humidity monitoring from MicroPython-based sensors
- **Document Conversion**: Convert DOCX documents to PDF using Syncfusion DocIO
- **Storage Analytics**: Real-time dashboard showing storage usage and capacity
- **Logging**: Comprehensive logging with Serilog to both console and file

## 🛠️ Tech Stack

### Backend
- **.NET 9.0**: Modern, high-performance framework
- **ASP.NET Core MVC**: Model-View-Controller architecture
- **Entity Framework Core**: Object-relational mapper for database operations
- **ASP.NET Identity**: User authentication and authorization

### Database
- **SQLite**: Lightweight, file-based database for easy deployment

### Libraries & Frameworks
- **MailKit**: Email sending functionality
- **Syncfusion DocIO**: Document processing and conversion
- **Serilog**: Structured logging
- **SerialPortStream**: Serial communication for IoT devices

### Frontend
- **Razor Pages**: Server-side rendered views
- **Bootstrap**: Responsive UI framework
- **Localization**: Built-in support for multiple languages

## 📦 Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 9.0 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Visual Studio 2022** (recommended) or **Visual Studio Code** with C# extension
- **Git** for version control
- **(Optional)** A SMTP server or Gmail account for email notifications
- **(Optional)** MicroPython-based IoT device for sensor data

## 🚀 Installation

### 1. Clone the Repository

```bash
git clone https://github.com/Stevekk11/PersonalCloud.git
cd PersonalCloud
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Database Setup

The application uses SQLite with an included `app.db` file. Database migrations are handled automatically on first run (commented out by default in `Program.cs`).

To manually apply migrations:

```bash
dotnet ef database update
```

### 4. Build the Application

```bash
dotnet build
```

### 5. Run the Application

```bash
dotnet run
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`.

## ⚙️ Configuration

Configuration is managed through `appsettings.json` and `appsettings.Development.json`. Here's what you need to configure:

### Database Connection

```json
"ConnectionStrings": {
  "DefaultConnection": "DataSource=app.db;Cache=Shared"
}
```

### Storage Configuration

```json
"Storage": {
  "Root": "UserDocs"
}
```

This sets the directory where uploaded documents will be stored. The directory will be created automatically if it doesn't exist.

### Email Configuration

For email notifications, configure your SMTP settings:

```json
"Email": {
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  },
  "From": "your-email@gmail.com"
}
```

**Important for Gmail users**: You'll need to use an [App Password](https://support.google.com/accounts/answer/185833) instead of your regular password.

### Sensor Server Configuration (Optional)

If you have a MicroPython-based temperature/humidity sensor:

```json
"SensorServer": {
  "Url": "http://192.168.1.90:5000"
}
```

### Logging Configuration

Logs are written to both console and files in the `Logs/` directory:

```json
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
}
```

## 📖 Usage

### First-Time Setup

1. **Navigate to the application** in your web browser (default: `https://localhost:5001`)
2. **Register a new account** using the Register link
3. **Login** with your credentials

### Uploading Documents

1. Navigate to the Documents section
2. Click "Upload Document"
3. Select your file(s) - supports up to 5 GB per file
4. Files are automatically validated for security
5. View your documents in the dashboard

### Document Management

- **View**: Click on a document to view or download
- **Delete**: Remove documents you no longer need
- **Convert to PDF**: DOCX files can be converted to PDF format
- **Storage Tracking**: Monitor your storage usage in the dashboard

### Multi-Language Support

Change the language by appending `?culture=en` (English) or `?culture=cs` (Czech) to any URL.

### Premium Features

Premium users receive 50 GB of storage instead of the standard 10 GB. Contact your administrator for premium access.


### Running in Development Mode

```bash
dotnet run --environment Development
```

This enables:
- Detailed error pages
- Database developer page exception filter
- Hot reload for faster development

### Running Tests

```bash
dotnet test
```

### Code Style

The project follows standard C# coding conventions:
- PascalCase for class names and public members
- camelCase for private fields (with `_` prefix)
- Comprehensive XML documentation comments
- Async/await for I/O operations

## 🚢 Deployment

### Publishing for Production

1. **Build for Release**:

```bash
dotnet publish -c Release -o ./publish
```

This creates a self-contained, single-file executable with compression enabled.

2. **Configure Production Settings**:

Create `appsettings.Production.json` with production-specific settings (do not commit sensitive data to version control).

3. **Environment Variables**:

Set `ASPNETCORE_ENVIRONMENT=Production` on your production server.

4. **Run the Application**:

```bash
cd publish
./PersonalCloud
```

## 🔧 Troubleshooting

### Common Issues

**Problem**: Application won't start
- **Solution**: Check that .NET 9.0 SDK is installed: `dotnet --version`
- **Solution**: Ensure port 5000/5001 is not in use by another application

**Problem**: Email notifications not working
- **Solution**: Verify SMTP settings in `appsettings.json`
- **Solution**: For Gmail, ensure you're using an App Password, not your account password
- **Solution**: Check firewall isn't blocking outbound SMTP connections

**Problem**: File upload fails
- **Solution**: Check available disk space
- **Solution**: Verify `UserDocs/` directory has write permissions
- **Solution**: Ensure file size is under 5 GB
- **Solution**: Check that storage quota hasn't been exceeded

**Problem**: Database errors
- **Solution**: Delete `app.db` and run `dotnet ef database update` to recreate
- **Solution**: Ensure application has read/write permissions to the database file

**Problem**: Sensor data not appearing
- **Solution**: Verify sensor server is running and accessible at the configured URL
- **Solution**: Check network connectivity to the sensor device
- **Solution**: Review logs in `Logs/` directory for sensor-related errors

### Logs

Check application logs in the `Logs/` directory for detailed error information. Logs are rotated daily.

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please ensure your code:
- Follows the existing code style
- Includes XML documentation comments for public APIs
- Passes all tests
- Includes appropriate error handling

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 👤 Author

Created and maintained by [Stevekk11](https://github.com/Stevekk11)

## 🙏 Acknowledgments

- Built with ASP.NET Core 9.0
- Uses Syncfusion components for document processing
- Email functionality powered by MailKit
- Logging by Serilog

---

For questions, issues, or feature requests, please [open an issue](https://github.com/Stevekk11/PersonalCloud/issues) on GitHub.
