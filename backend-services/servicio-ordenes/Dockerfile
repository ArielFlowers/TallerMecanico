ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

# El contexto es la raiz del repositorio, para incluir los DTO compartidos.
COPY shared/TallerMecanico.Contratos/TallerMecanico.Contratos.csproj shared/TallerMecanico.Contratos/
COPY backend-services/servicio-ordenes/TallerMecanico.ServicioOrdenes.csproj backend-services/servicio-ordenes/
RUN dotnet restore backend-services/servicio-ordenes/TallerMecanico.ServicioOrdenes.csproj
COPY shared/ shared/
COPY backend-services/servicio-ordenes/ backend-services/servicio-ordenes/
RUN dotnet publish backend-services/servicio-ordenes/TallerMecanico.ServicioOrdenes.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
# Compose habilita HTTPS en 8443 con el certificado interno.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080 8443
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TallerMecanico.ServicioOrdenes.dll"]
