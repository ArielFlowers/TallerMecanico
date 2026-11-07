ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

COPY shared/TallerMecanico.Contratos/TallerMecanico.Contratos.csproj shared/TallerMecanico.Contratos/
COPY backend-services/servicio-usuarios/TallerMecanico.ServicioUsuarios.csproj backend-services/servicio-usuarios/
RUN dotnet restore backend-services/servicio-usuarios/TallerMecanico.ServicioUsuarios.csproj
COPY shared/ shared/
COPY backend-services/servicio-usuarios/ backend-services/servicio-usuarios/
RUN dotnet publish backend-services/servicio-usuarios/TallerMecanico.ServicioUsuarios.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TallerMecanico.ServicioUsuarios.dll"]
