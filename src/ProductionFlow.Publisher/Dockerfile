FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build

WORKDIR /src

COPY global.json ./

COPY src/ProductionFlow.Messaging/ProductionFlow.Messaging.csproj src/ProductionFlow.Messaging/
COPY src/ProductionFlow.Publisher/ProductionFlow.Publisher.csproj src/ProductionFlow.Publisher/

RUN dotnet restore src/ProductionFlow.Publisher/ProductionFlow.Publisher.csproj

COPY src/ProductionFlow.Messaging/ src/ProductionFlow.Messaging/
COPY src/ProductionFlow.Publisher/ src/ProductionFlow.Publisher/

RUN dotnet publish src/ProductionFlow.Publisher/ProductionFlow.Publisher.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS final

WORKDIR /app

COPY --from=build /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "ProductionFlow.Publisher.dll"]