FROM mcr.microsoft.com/dotnet/core/sdk AS build-env

WORKDIR /usr/src/rydrhangfire

COPY src/RydrHangfire* ./src

WORKDIR /usr/src/rydrhangfire/src

RUN dotnet restore
RUN dotnet publish -c Release -o publish

################################################################
# Deployment
################################################################
FROM mcr.microsoft.com/dotnet/core/aspnet

RUN apt-get update -y && apt-get upgrade -y && apt-get install -y curl libc6-dev libgdiplus && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/rydrhangfire

COPY --from=build-env /usr/src/rydrhangfire/src/publish /opt/rydrhangfire

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:2081

EXPOSE 2081

ENTRYPOINT ["/opt/rydrhangfire/rydrhangfire"]
