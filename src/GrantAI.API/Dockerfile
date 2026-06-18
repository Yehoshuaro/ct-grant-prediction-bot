# Build context is the repository root (see docker-compose.yml).
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the csproj files first so this layer is cached unless a
# project file changes. Directory.Build.props carries the shared TFM/settings.
COPY Directory.Build.props ./
COPY src/GrantAI.Domain/GrantAI.Domain.csproj            src/GrantAI.Domain/
COPY src/GrantAI.Application/GrantAI.Application.csproj   src/GrantAI.Application/
COPY src/GrantAI.Infrastructure/GrantAI.Infrastructure.csproj src/GrantAI.Infrastructure/
COPY src/GrantAI.API/GrantAI.API.csproj                  src/GrantAI.API/
RUN dotnet restore src/GrantAI.API/GrantAI.API.csproj

# Copy the rest of the sources and publish.
COPY src/ src/
RUN dotnet publish src/GrantAI.API/GrantAI.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Listen on 8080 inside the container (mapped in compose).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GrantAI.API.dll"]
