# DealerFOSS — the whole application in one image.
#
# Build from the REPOSITORY ROOT, not from this folder:
#   docker build -f deploy/Dockerfile -t dealerfoss:local .
#
# Edit: the frontend is built here rather than expected to exist. An image that
#       depended on somebody having run `npm run build` first would sometimes be
#       an image with an empty wwwroot — which starts, answers health, serves the
#       API, and shows a blank page. The build has to be part of the build.
#
#       Three stages so the runtime image carries neither Node nor the .NET SDK.

# --- the frontend -------------------------------------------------------------

FROM node:22-alpine AS frontend
WORKDIR /src/frontend

# The manifests first, so a change to application code does not re-run the
# install. npm ci and not npm install: the lockfile is the point.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY frontend/ ./
RUN npm run build

# Fails the build here rather than shipping a blank page.
RUN test -f dist/index.html

# --- the application ----------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS backend
WORKDIR /src

# Restore against the manifests alone, so the layer survives a code change.
COPY Directory.Build.props Directory.Packages.props nuget.config* ./
COPY src/Core/Core.csproj src/Core/
COPY src/Identity/Identity.csproj src/Identity/
COPY src/App/App.csproj src/App/
RUN dotnet restore src/App/App.csproj

COPY src/ src/

# The built page goes where the application serves from. Copied AFTER the source
# so a frontend-only change does not invalidate the .NET restore layer.
COPY --from=frontend /src/frontend/dist/ src/App/wwwroot/

RUN dotnet publish src/App/App.csproj -c Release -o /app --no-restore --nologo

RUN test -f /app/wwwroot/index.html

# --- what actually ships ------------------------------------------------------

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

# Not root. The application writes nothing to its own directory, so it does not
# need to own one.
USER $APP_UID

COPY --from=backend /app ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Liveness only. Readiness needs the database, and a container that reports
# unhealthy because SQL Server is still starting is a container an orchestrator
# will keep killing and restarting for no reason.
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD ["/bin/sh", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.0\\r\\n\\r\\n' >&3 && head -1 <&3 | grep -q 200"]

ENTRYPOINT ["dotnet", "DealerFOSS.App.dll"]
