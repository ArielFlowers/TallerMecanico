
# ETAPA 1: Compilación de la aplicación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Restaurar dependencias
COPY TallerMecanico.csproj ./
RUN dotnet restore TallerMecanico.csproj

# Copiar el código fuente
COPY . .

# Publicar la aplicación
RUN dotnet publish TallerMecanico.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ETAPA 2: Imagen de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Puerto interno compartido por todas las réplicas
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Copiar la aplicación compilada
COPY --from=build /app/publish .

# Iniciar AutoTaller Pro
ENTRYPOINT ["dotnet", "TallerMecanico.dll"]
