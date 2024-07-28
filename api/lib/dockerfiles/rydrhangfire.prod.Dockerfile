FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /usr/src/rydrhangfire

COPY src/RydrHangfire* ./src

WORKDIR /usr/src/rydrhangfire/src

RUN dotnet restore
RUN dotnet publish -c Release -o publish

################################################################
# Deployment
################################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /opt/rydrhangfire

COPY --from=build-env /usr/src/rydrhangfire/src/publish /opt/rydrhangfire

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:2081
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0

EXPOSE 2081

ENTRYPOINT ["/opt/rydrhangfire/rydrhangfire"]
