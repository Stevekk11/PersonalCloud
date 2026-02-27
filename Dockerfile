# Use the official .NET runtime image for the base stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the official .NET SDK image for the build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["PersonalCloud.csproj", "."]
RUN dotnet restore "./PersonalCloud.csproj"

# Copy the rest of the application code
COPY . .
WORKDIR "/src/."
RUN dotnet build "PersonalCloud.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PersonalCloud.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage: build the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install native dependencies for Syncfusion and System.Drawing.Common
# libgdiplus is often needed for various image/PDF operations
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libx11-6 \
    libc6-dev \
    libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

# Create directories for persistent storage and set permissions for the 'app' user
USER root
RUN mkdir -p /app/UserDocs /app/Logs /app/data && \
    chown -R 1654:1654 /app/UserDocs /app/Logs /app/data
USER app

ENTRYPOINT ["dotnet", "PersonalCloud.dll"]



