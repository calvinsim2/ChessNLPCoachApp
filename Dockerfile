
# STAGE 1: Build the .NET 10 application.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Work inside the source directory.
WORKDIR /src

# Copy the project definition.
COPY ChessNLPCoachApp/ChessNLPCoachApp.csproj ChessNLPCoachApp/

# Restore the NuGet dependencies.
RUN dotnet restore ChessNLPCoachApp/ChessNLPCoachApp.csproj

# Copy the rest of the source code.
COPY . .

# Publish the release version of the application.
RUN dotnet publish ChessNLPCoachApp/ChessNLPCoachApp.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# STAGE 2: Create the runtime container.
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Install Stockfish during image creation.
USER root

# Download the official Linux x86-64 Stockfish 19 release.
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        ca-certificates curl libstdc++6; \
    mkdir -p /tmp/stockfish; \
    curl -fsSL \
        https://github.com/official-stockfish/Stockfish/releases/download/sf_19/stockfish-linux-x86-64-universal.tar.gz \
        -o /tmp/stockfish.tar.gz; \
    tar -xzf /tmp/stockfish.tar.gz -C /tmp/stockfish; \
    STOCKFISH_BINARY="$(find /tmp/stockfish -type f -name 'stockfish-linux-x86-64-universal' -print -quit)"; \
    test -n "$STOCKFISH_BINARY"; \
    install -m 0755 "$STOCKFISH_BINARY" /usr/local/bin/stockfish; \
    rm -rf /tmp/stockfish /tmp/stockfish.tar.gz; \
    rm -rf /var/lib/apt/lists/*

# Set the application directory.
WORKDIR /app

# Copy the published .NET application.
COPY --from=build /app/publish .

# Configure the ASP.NET Core HTTP port.
ENV ASPNETCORE_HTTP_PORTS=8080

# Configure the Linux Stockfish path.
ENV Stockfish__Path=/usr/local/bin/stockfish

# Document the listening port.
EXPOSE 8080

# Run the application without root privileges.
USER $APP_UID

# Start the MVC application.
ENTRYPOINT ["dotnet", "ChessNLPCoachApp.dll"]