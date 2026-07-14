# Multi-stage build for Railway. Railway auto-detects and builds this
# Dockerfile — no extra config needed beyond the Root Directory setting
# (see the deployment guide for what to set that to).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Launchly.API.csproj ./
RUN dotnet restore Launchly.API.csproj

COPY . .
RUN dotnet publish Launchly.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects a $PORT env var at container start (its value isn't known
# at build time, so it can't be baked into ASPNETCORE_URLS above) — this
# binds Kestrel to whatever port Railway actually routes traffic to.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Launchly.API.dll"]
