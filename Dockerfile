# Imagenes base parametrizadas para poder fijarlas por digest.
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

# ETAPA 1: Compilacion de la aplicacion
FROM ${DOTNET_SDK_IMAGE} AS build

WORKDIR /src

# Restaurar dependencias
COPY TallerMecanico.csproj ./
RUN dotnet restore TallerMecanico.csproj

# Copiar el codigo fuente
COPY . .

# Publicar la aplicacion
RUN dotnet publish TallerMecanico.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ETAPA 2: Imagen de ejecucion
FROM ${DOTNET_RUNTIME_IMAGE} AS runtime

WORKDIR /app

# Puerto interno compartido por todas las replicas
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Copiar la aplicacion compilada
COPY --from=build /app/publish .

# Iniciar AutoTaller Pro
ENTRYPOINT ["dotnet", "TallerMecanico.dll"]
