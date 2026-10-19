FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build

WORKDIR /src

COPY global.json ./

COPY src/ProductionFlow.Messaging/ProductionFlow.Messaging.csproj src/ProductionFlow.Messaging/
COPY src/ProductionFlow.Worker/ProductionFlow.Worker.csproj src/ProductionFlow.Worker/

RUN dotnet restore src/ProductionFlow.Worker/ProductionFlow.Worker.csproj

COPY src/ProductionFlow.Messaging/ src/ProductionFlow.Messaging/
COPY src/ProductionFlow.Worker/ src/ProductionFlow.Worker/

RUN dotnet publish src/ProductionFlow.Worker/ProductionFlow.Worker.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS final

WORKDIR /app

COPY --from=build /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "ProductionFlow.Worker.dll"]