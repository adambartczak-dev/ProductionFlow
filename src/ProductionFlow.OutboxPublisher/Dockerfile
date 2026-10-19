# Etap 1: kompilacja
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build

WORKDIR /src

COPY global.json ./

# Projekty wymagane do przywrócenia pakietów.
COPY src/ProductionFlow.Messaging/ProductionFlow.Messaging.csproj \
     src/ProductionFlow.Messaging/

COPY src/ProductionFlow.Api/ProductionFlow.Api.csproj \
     src/ProductionFlow.Api/

COPY src/ProductionFlow.OutboxPublisher/ProductionFlow.OutboxPublisher.csproj \
     src/ProductionFlow.OutboxPublisher/

RUN dotnet restore \
    src/ProductionFlow.OutboxPublisher/ProductionFlow.OutboxPublisher.csproj

# Kopiowanie kodu.
COPY src/ProductionFlow.Messaging/ \
     src/ProductionFlow.Messaging/

COPY src/ProductionFlow.Api/ \
     src/ProductionFlow.Api/

COPY src/ProductionFlow.OutboxPublisher/ \
     src/ProductionFlow.OutboxPublisher/

# Publikacja aplikacji.
RUN dotnet publish \
    src/ProductionFlow.OutboxPublisher/ProductionFlow.OutboxPublisher.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Etap 2: uruchamianie
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

WORKDIR /app

COPY --from=build /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "ProductionFlow.OutboxPublisher.dll"]