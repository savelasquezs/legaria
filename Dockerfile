FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY backend/Legaria.sln backend/
COPY backend/src/Legaria.Domain/Legaria.Domain.csproj backend/src/Legaria.Domain/
COPY backend/src/Legaria.Application/Legaria.Application.csproj backend/src/Legaria.Application/
COPY backend/src/Legaria.Infrastructure/Legaria.Infrastructure.csproj backend/src/Legaria.Infrastructure/
COPY backend/src/Legaria.API/Legaria.API.csproj backend/src/Legaria.API/
RUN dotnet restore backend/src/Legaria.API/Legaria.API.csproj

COPY backend/ backend/

FROM restore AS build
RUN dotnet build backend/src/Legaria.API/Legaria.API.csproj \
    --configuration Release \
    --no-restore

FROM build AS publish
RUN dotnet publish backend/src/Legaria.API/Legaria.API.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS migrations
RUN dotnet tool install --tool-path /tools dotnet-ef --version 8.0.11
ENTRYPOINT ["/tools/dotnet-ef"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8081
ENTRYPOINT ["dotnet", "Legaria.API.dll"]
