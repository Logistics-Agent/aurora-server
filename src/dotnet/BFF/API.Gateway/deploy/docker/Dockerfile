# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble AS build
WORKDIR /src

COPY ["src/dotnet/BFF/API.Gateway/API.Gateway.csproj", "src/dotnet/BFF/API.Gateway/"]
RUN dotnet restore "src/dotnet/BFF/API.Gateway/API.Gateway.csproj"

COPY src/dotnet/BFF/API.Gateway/ src/dotnet/BFF/API.Gateway/
WORKDIR "/src/src/dotnet/BFF/API.Gateway"
RUN dotnet publish "API.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "API.Gateway.dll"]
