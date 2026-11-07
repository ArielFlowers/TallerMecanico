# Imagenes base parametrizadas para poder fijarlas por digest.
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

# ETAPA 1: Compilacion de la aplicacion
FROM ${DOTNET_SDK_IMAGE} AS build

WORKDIR /src

# El contexto de construccion es la raiz del monorepo.
# Restaurar dependencias
COPY shared/TallerMecanico.Contratos/TallerMecanico.Contratos.csproj shared/TallerMecanico.Contratos/
COPY frontend-app/TallerMecanico.Frontend.csproj frontend-app/
RUN dotnet restore frontend-app/TallerMecanico.Frontend.csproj

# Copiar el codigo fuente
COPY shared/ shared/
COPY frontend-app/ frontend-app/

# Publicar la aplicacion
RUN dotnet publish frontend-app/TallerMecanico.Frontend.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ETAPA 2: Imagen de ejecucion
FROM ${DOTNET_RUNTIME_IMAGE} AS runtime

WORKDIR /app

# curl permite comprobar /salud sin depender del SDK dentro de la imagen.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Compose configura el certificado PEM y habilita HTTPS en 8443.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080 8443

# Copiar la aplicacion compilada
COPY --from=build /app/publish .

# Iniciar AutoTaller Pro
ENTRYPOINT ["dotnet", "TallerMecanico.Frontend.dll"]
