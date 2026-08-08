# syntax=docker/dockerfile:1

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
#
#       BuildKit runs independent stages IN PARALLEL, so on a first build the two
#       base-image pulls (~250 MB) and the npm install compete for the same link.
#       On a slow connection npm loses: its default fetch-timeout is exactly five
#       minutes, and a first build here died at 299.4s with ECONNRESET. Hence the
#       explicit retry policy and the cache mounts below — the retries survive a
#       reset, and the caches mean a retry does not start from nothing.

# --- the frontend -------------------------------------------------------------

FROM node:22-alpine AS frontend
WORKDIR /src/frontend

# The manifests first, so a change to application code does not re-run the
# install. npm ci and not npm install: the lockfile is the point.
COPY frontend/package.json frontend/package-lock.json ./

# The cache mount is what makes a retry cheap: packages already fetched are still
# there on the second attempt. It lives outside the image, so it costs the
# runtime nothing.
RUN --mount=type=cache,target=/root/.npm \
    npm ci --no-audit --no-fund \
      --fetch-retries=5 \
      --fetch-retry-mintimeout=20000 \
      --fetch-retry-maxtimeout=180000 \
      --fetch-timeout=900000

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
#
# Same reasoning as npm above: cached packages survive a failed attempt. Both
# this and the publish below must mount it — with the packages in a cache rather
# than in a layer, a --no-restore publish without the mount cannot find them.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/App/App.csproj

COPY src/ src/

# The built page goes where the application serves from. Copied AFTER the source
# so a frontend-only change does not invalidate the .NET restore layer.
COPY --from=frontend /src/frontend/dist/ src/App/wwwroot/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/App/App.csproj -c Release -o /app --no-restore --nologo

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
