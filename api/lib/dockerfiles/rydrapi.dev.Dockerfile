FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /usr/src/rydrapi

COPY lib/ ./lib
COPY src/ ./src
COPY tests/ ./tests

WORKDIR /usr/src/rydrapi/src

RUN dotnet restore
RUN dotnet publish -c Development -o publish

################################################################
# Deployment
################################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /opt/rydrapi

COPY --from=build-env /usr/src/rydrapi/src/publish /opt/rydrapi

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:2080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0

EXPOSE 2080

ENTRYPOINT ["/opt/rydrapi/rydrapi"]
