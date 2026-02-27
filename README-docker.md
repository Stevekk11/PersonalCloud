# Dockerizing PersonalCloud

This project is now dockerized and can be easily run using Docker and Docker Compose.

## How to run

1.  Make sure you have [Docker](https://www.docker.com/get-started) installed.
2.  Open a terminal in the project root directory.
3.  Build and start the container:
    ```bash
    docker-compose up -d --build
    ```
4.  The application will be available at `http://localhost:8080`.

## Persistent Data

The following directories are mounted as volumes to ensure data persistence:
- `./UserDocs`: Stores uploaded user documents.
- `./Logs`: Stores application logs.
- `./data`: Stores the SQLite database (`app.db`).

## Configuration

You can configure the application by editing the `environment` section in `docker-compose.yml`. Key settings include:
- `Email__Smtp__*`: SMTP settings for sending emails.
- `SensorServer__Url`: URL of the IoT sensor server.
- `SYNCFUSION_LICENSE_KEY`: Your Syncfusion license key.
- `ASPNETCORE_ENVIRONMENT`: Set to `Production` or `Development`.

## Troubleshooting

- **Permissions**: On Linux hosts, you might need to ensure the `UserDocs`, `Logs`, and `data` directories have the correct permissions for the `app` user (UID 1654) or run as root. The `Dockerfile` attempts to set these up, but host-mounted volumes can sometimes override them.
- **System.Drawing.Common**: The application uses `System.Drawing.Common`. While the `Dockerfile` installs `libgdiplus`, .NET 9 has limited support for this on Linux. If image features (like getting dimensions) fail, consider using a cross-platform library like ImageSharp in the future.

