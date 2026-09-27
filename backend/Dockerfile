# syntax=docker/dockerfile:1
# -----------------------------------------------------------------------------
# IPL Store API image. Build context = REPOSITORY ROOT (needs /database too):
#   docker build -f backend/Dockerfile -t iplstore-api .
# Multi-stage: SDK to build, slim ASP.NET runtime to run, as a non-root user.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# 1) Restore in its own layer: only re-runs when a project/package file changes.
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/
COPY backend/src/IplStore.Domain/IplStore.Domain.csproj                 backend/src/IplStore.Domain/
COPY backend/src/IplStore.Application/IplStore.Application.csproj       backend/src/IplStore.Application/
COPY backend/src/IplStore.Infrastructure/IplStore.Infrastructure.csproj backend/src/IplStore.Infrastructure/
COPY backend/src/IplStore.Api/IplStore.Api.csproj                       backend/src/IplStore.Api/
RUN dotnet restore backend/src/IplStore.Api/IplStore.Api.csproj

# 2) Build + publish.
COPY backend/ backend/
COPY database/ database/
RUN dotnet publish backend/src/IplStore.Api/IplStore.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app .
# Built-in non-root user of the official .NET 8 images.
USER $APP_UID
ENTRYPOINT ["dotnet", "IplStore.Api.dll"]
# Same image runs the migration job in CD:  docker run iplstore-api --migrate-only
