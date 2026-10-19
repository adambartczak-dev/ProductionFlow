# Etap 1: kompilacja aplikacji .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build

WORKDIR /src

# Wersja SDK
COPY global.json ./

# Pliki projektów potrzebne do restore
COPY src/ProductionFlow.Messaging/ProductionFlow.Messaging.csproj \
     src/ProductionFlow.Messaging/

COPY src/ProductionFlow.Api/ProductionFlow.Api.csproj \
     src/ProductionFlow.Api/

RUN dotnet restore \
    src/ProductionFlow.Api/ProductionFlow.Api.csproj

# Kod obu projektów
COPY src/ProductionFlow.Messaging/ \
     src/ProductionFlow.Messaging/

COPY src/ProductionFlow.Api/ \
     src/ProductionFlow.Api/

# Publikacja API
RUN dotnet publish \
    src/ProductionFlow.Api/ProductionFlow.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Etap 2: uruchomienie API
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

# Narzędzie dla healthcheck
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ProductionFlow.Api.dll"]