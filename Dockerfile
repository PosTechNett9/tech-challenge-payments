ARG DOTNET_VERSION=8.0
ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
ARG DOTNET_VERSION
ARG BUILD_CONFIGURATION
WORKDIR /src

COPY FIAP.CloudGames.Payments.sln ./
COPY src/FIAP.CloudGames.Payments.API/FIAP.CloudGames.Payments.API.csproj src/FIAP.CloudGames.Payments.API/
COPY src/FIAP.CloudGames.Payments.Application/FIAP.CloudGames.Payments.Application.csproj src/FIAP.CloudGames.Payments.Application/
COPY src/FIAP.CloudGames.Payments.Domain/FIAP.CloudGames.Payments.Domain.csproj src/FIAP.CloudGames.Payments.Domain/
COPY src/FIAP.CloudGames.Payments.Infrastructure/FIAP.CloudGames.Payments.Infrastructure.csproj src/FIAP.CloudGames.Payments.Infrastructure/

RUN dotnet restore src/FIAP.CloudGames.Payments.API/FIAP.CloudGames.Payments.API.csproj
COPY src/ ./src/

RUN dotnet publish src/FIAP.CloudGames.Payments.API/FIAP.CloudGames.Payments.API.csproj \
    -c ${BUILD_CONFIGURATION} -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS="http://+:8080" \
    ASPNETCORE_ENVIRONMENT="Production" \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build --chown=app:app /app/publish/ ./

USER app
ENTRYPOINT ["dotnet", "FIAP.CloudGames.Payments.API.dll"]