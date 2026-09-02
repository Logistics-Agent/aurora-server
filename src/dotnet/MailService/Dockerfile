FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Layer-cached NuGet restore
COPY protos/ protos/
COPY src/dotnet/shared/shared.csproj src/dotnet/shared/
COPY src/dotnet/MailService/MailService.csproj src/dotnet/MailService/

RUN dotnet restore src/dotnet/MailService/MailService.csproj

# Copy source tree and compile Release bundle
COPY src/dotnet/shared/ src/dotnet/shared/
COPY src/dotnet/MailService/ src/dotnet/MailService/

WORKDIR /src/src/dotnet/MailService
RUN dotnet publish MailService.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for container liveness healthcheck probe
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy compiled binaries from build stage
COPY --from=build /app/publish .

# Environment and Ports
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5003
EXPOSE 9090

# Non-root execution
USER app

# Liveness healthcheck (probes runtime process health without external DB restart cascade)
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:9090/health/live || exit 1

ENTRYPOINT ["dotnet", "MailService.dll"]
