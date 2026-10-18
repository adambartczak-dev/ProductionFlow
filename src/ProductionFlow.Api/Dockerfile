
# Etap 1: kompilacja aplikacji .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build

WORKDIR /src

# Wersja SDK używana w projekcie.
COPY global.json ./

# Plik projektu potrzebny do restore.
COPY src/ProductionFlow.Api/ProductionFlow.Api.csproj \
     src/ProductionFlow.Api/

RUN dotnet restore \
    src/ProductionFlow.Api/ProductionFlow.Api.csproj

# Kod aplikacji.
COPY src/ProductionFlow.Api/ src/ProductionFlow.Api/

# Kompilacja w konfiguracji Release.
RUN dotnet publish \
    src/ProductionFlow.Api/ProductionFlow.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Etap 2: lekki obraz do uruchomienia API
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

# Narzędzie wykorzystywane przez healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Uruchamiamy aplikację bez uprawnień root.
USER $APP_UID

ENTRYPOINT ["dotnet", "ProductionFlow.Api.dll"]
