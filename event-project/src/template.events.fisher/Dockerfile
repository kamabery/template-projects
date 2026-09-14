# Build context is the repository root, not this directory:
#
#   docker build -f src/template.events.fisher/Dockerfile -t template-events .
#
# The project references template.servicedefaults one level up and compiles src/shared/ in through
# a Compile glob, so nothing narrower than the root works.
#
# This file describes the scaffolded layout, where the events slice sits under src/ beside the API.
# In the source workspace the slice is a sibling of base-project/ and this build will not run - that
# is what staging fixes, and it is the same reason the csproj's servicedefaults path is rewritten
# there.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/ src/

RUN dotnet restore src/template.events.fisher/template.events.fisher.csproj
RUN dotnet publish src/template.events.fisher/template.events.fisher.csproj       --configuration Release       --no-restore       --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080

COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "template.events.fisher.dll"]
