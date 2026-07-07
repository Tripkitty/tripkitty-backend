FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy Central Package Management and all csproj files first (layer cache for restore)
COPY Directory.Packages.props .
COPY ["Tripkitty.Api/Tripkitty.Api.csproj", "Tripkitty.Api/"]
COPY ["Tripkitty.Application/Tripkitty.Application.csproj", "Tripkitty.Application/"]
COPY ["Tripkitty.Infrastructure/Tripkitty.Infrastructure.csproj", "Tripkitty.Infrastructure/"]
COPY ["Tripkitty.Domain/Tripkitty.Domain.csproj", "Tripkitty.Domain/"]
RUN dotnet restore "Tripkitty.Api/Tripkitty.Api.csproj"

COPY . .
WORKDIR "/src/Tripkitty.Api"
RUN dotnet build "./Tripkitty.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tripkitty.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
USER $APP_UID
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tripkitty.Api.dll"]
