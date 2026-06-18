FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/GrantAI.Domain/GrantAI.Domain.csproj            src/GrantAI.Domain/
COPY src/GrantAI.Application/GrantAI.Application.csproj   src/GrantAI.Application/
COPY src/GrantAI.Infrastructure/GrantAI.Infrastructure.csproj src/GrantAI.Infrastructure/
COPY src/GrantAI.Bot/GrantAI.Bot.csproj                  src/GrantAI.Bot/
RUN dotnet restore src/GrantAI.Bot/GrantAI.Bot.csproj
COPY src/ src/
RUN dotnet publish src/GrantAI.Bot/GrantAI.Bot.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "GrantAI.Bot.dll"]
