# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble AS build
WORKDIR /src

COPY ["protos/", "protos/"]
COPY ["src/dotnet/shared/shared.csproj", "src/dotnet/shared/"]
COPY ["src/dotnet/MailService/MailService.csproj", "src/dotnet/MailService/"]

RUN dotnet restore "src/dotnet/MailService/MailService.csproj"

COPY src/dotnet/shared/ src/dotnet/shared/
COPY src/dotnet/MailService/ src/dotnet/MailService/

WORKDIR "/src/src/dotnet/MailService"
RUN dotnet publish "MailService.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 5003
ENV ASPNETCORE_URLS=http://+:5003
ENTRYPOINT ["dotnet", "MailService.dll"]
