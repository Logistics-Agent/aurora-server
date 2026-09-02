# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble AS build
WORKDIR /src

COPY ["protos/", "protos/"]
COPY ["src/dotnet/shared/shared.csproj", "src/dotnet/shared/"]
COPY ["src/dotnet/Notification/Notification.csproj", "src/dotnet/Notification/"]

RUN dotnet restore "src/dotnet/Notification/Notification.csproj"

COPY src/dotnet/shared/ src/dotnet/shared/
COPY src/dotnet/Notification/ src/dotnet/Notification/

WORKDIR "/src/src/dotnet/Notification"
RUN dotnet publish "Notification.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "Notification.dll"]
