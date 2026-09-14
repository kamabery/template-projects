# Build context is the repository root, not this directory:
#
#   docker build -f src/template.api/Dockerfile -t template-api .
#
# The project references template.data, template.servicedefaults and the migrations project, and
# reads Directory.Build.props, Directory.Packages.props and nuget.config from the root - none of
# which are visible from a context rooted here.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copied whole rather than csproj-by-csproj. Which projects exist depends on which data store this
# was scaffolded with, so a restore layer that named them individually would be wrong in two of the
# three configurations - and being right everywhere is worth more here than one cached layer.
COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/ src/

RUN dotnet restore src/template.api/template.api.csproj
RUN dotnet publish src/template.api/template.api.csproj \
      --configuration Release \
      --no-restore \
      --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The aspnet image listens on 8080 by default, which is what the container app's ingress and both
# probes target. There is no https here on purpose: TLS terminates at the Container Apps ingress,
# and the API deliberately has no UseHttpsRedirection to be confused by that.
EXPOSE 8080

COPY --from=build /app .

# The image's own non-root user. On the SQLite branch the Azure Files share is mounted
# world-writable, so this user can still write /data/app.db.
USER $APP_UID

ENTRYPOINT ["dotnet", "template.api.dll"]
