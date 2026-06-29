FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Tripkitty.Api/Tripkitty.Api.csproj", "Tripkitty.Api/"]
RUN dotnet restore "Tripkitty.Api/Tripkitty.Api.csproj"
COPY . .
WORKDIR "/src/Tripkitty.Api"
RUN dotnet build "./Tripkitty.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tripkitty.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tripkitty.Api.dll"]
