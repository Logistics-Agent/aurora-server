# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble AS build
WORKDIR /src

COPY ["protos/", "protos/"]
COPY ["src/dotnet/shared/shared.csproj", "src/dotnet/shared/"]
COPY ["src/dotnet/RoutePlanningAgent/RoutePlanningAgent.csproj", "src/dotnet/RoutePlanningAgent/"]

RUN dotnet restore "src/dotnet/RoutePlanningAgent/RoutePlanningAgent.csproj"

COPY src/dotnet/shared/ src/dotnet/shared/
COPY src/dotnet/RoutePlanningAgent/ src/dotnet/RoutePlanningAgent/

WORKDIR "/src/src/dotnet/RoutePlanningAgent"
RUN dotnet publish "RoutePlanningAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "RoutePlanningAgent.dll"]
