ARG DOTNET_RUNTIME=mcr.microsoft.com/dotnet/aspnet:10.0-alpine
ARG DOTNET_SDK=mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb


FROM ${DOTNET_RUNTIME} AS base
USER app
WORKDIR /app
##EXPOSE 5000
# EXPOSE 5001

# Base for build
FROM ${DOTNET_SDK} AS build
ARG BUILD_CONFIGURATION=Release
ARG PROJECT_PATH=SpotTool.Web.csproj
WORKDIR /src

COPY SpotTool.Web/SpotTool.Web.csproj SpotTool.Web/

RUN dotnet restore "./SpotTool.Web/SpotTool.Web.csproj"

COPY . .

WORKDIR /src/SpotTool.Web

##RUN dotnet tool install --version 9.0.3 --global dotnet-ef
##ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet build "./SpotTool.Web.csproj" -c ${BUILD_CONFIGURATION} -o /app/build
##RUN dotnet tool restore
##RUN dotnet ef database update --project ./

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG PROJECT_PATH=./SpotTool.Web.csproj

RUN dotnet publish "./SpotTool.Web.csproj" -c ${BUILD_CONFIGURATION} --os linux-musl --arch x64 -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

USER root
RUN apk add --no-cache icu-libs curl

# > [spottool.api final 2/3] RUN apk add --no-cache icu-libs:
#     -0.473 ERROR: Unable to lock database: Permission denied
#     -0.471 ERROR: Failed to open apk database: Permission denied
#     ------
#     failed to solve: process "/bin/sh -c apk add --no-cache icu-libs" did not complete successfully: exit code: 99
USER app
COPY --from=publish /app/publish .
ENTRYPOINT [ "dotnet", "SpotTool.Web.dll" ]