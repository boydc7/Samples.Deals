# docker build -f lib/dockerfiles/rydrapi.local.Dockerfile -t local/rydr-api .
FROM mcr.microsoft.com/dotnet/core/sdk AS build-env

WORKDIR /usr/src/rydrapi

COPY lib/ ./lib
COPY src/ ./src
COPY tests/ ./tests

WORKDIR /usr/src/rydrapi/src

RUN dotnet restore
RUN dotnet publish -c LocalDocker -o publish

################################################################
# Deployment
################################################################
FROM mcr.microsoft.com/dotnet/core/aspnet

RUN apt-get update -y && apt-get upgrade -y && apt-get install -y curl libc6-dev libgdiplus && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/rydrapi

COPY --from=build-env /usr/src/rydrapi/src/publish /opt/rydrapi

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:2080

EXPOSE 2080

ENTRYPOINT ["/opt/rydrapi/rydrapi"]
