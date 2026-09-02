# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble AS build
WORKDIR /src

COPY ["protos/", "protos/"]
COPY ["src/dotnet/shared/shared.csproj", "src/dotnet/shared/"]
COPY ["src/dotnet/BFF/BuildingBlocks.BFF/BuildingBlocks.BFF.csproj", "src/dotnet/BFF/BuildingBlocks.BFF/"]
COPY ["src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj", "src/dotnet/BFF/Staff.Bff/"]

RUN dotnet restore "src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj"

COPY src/dotnet/shared/ src/dotnet/shared/
COPY src/dotnet/BFF/BuildingBlocks.BFF/ src/dotnet/BFF/BuildingBlocks.BFF/
COPY src/dotnet/BFF/Staff.Bff/ src/dotnet/BFF/Staff.Bff/

WORKDIR "/src/src/dotnet/BFF/Staff.Bff"
RUN dotnet publish "Staff.Bff.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Staff.Bff.dll"]
