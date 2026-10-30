
# H1 - Docker, Nginx y HTTPS

## 1. Descripción

AutoTaller Pro utiliza Docker Compose para ejecutar tres réplicas
de una aplicación monolítica ASP.NET Core .NET 10, con MySQL
como base de datos y Nginx como balanceador de carga.

La arquitectura permite distribuir solicitudes entre las tres
réplicas y mantener la autenticación mediante cookies compartidas.

## 2. Arquitectura

- ASP.NET Core .NET 10
- MySQL 8.4
- Tres réplicas: app1, app2 y app3
- Nginx con balanceo Round Robin
- Data Protection con claves compartidas
- HTTP en puerto 80
- HTTPS en puerto 443
- Certificados locales generados mediante mkcert

## 3. Instalación y arranque

Los requisitos, `.env`, generación de certificados con mkcert y comandos de
arranque se mantienen en [README.md](../README.md). Usar
`dotnet msbuild -t:NubeUp`.
El Compose construye la imagen desde el código de la PC y conecta a Aiven.

## 4. Direcciones disponibles

- HTTP: http://127.0.0.1
- HTTPS: https://127.0.0.1
- HTTP alternativo: http://127.0.0.1:8088

Acceso directo a las réplicas:

- app1: http://127.0.0.1:5000
- app2: http://127.0.0.1:5001
- app3: http://127.0.0.1:5002

Estas direcciones son de la aplicación en esta PC; la base está en Aiven.

## 5. Prueba de balanceo de carga

Ejecutar:

    1..9 | ForEach-Object { (Invoke-RestMethod https://127.0.0.1/h1/replica).replica }

Se esperan identificadores diferentes correspondientes a las
tres réplicas.

El dashboard también muestra la réplica activa.

## 6. Prueba de tolerancia a fallos

Detener la primera réplica:

    docker compose -f docker-compose.yml stop app1

Comprobar que la aplicación sigue respondiendo por HTTPS
a través de app2 y app3.

Restaurar la réplica:

    docker compose -f docker-compose.yml start app1

Conservar el volumen de claves y los recursos históricos. No utilizar
docker compose down -v. Los datos de trabajo están en Aiven.

## 7. Autenticación compartida

Las tres réplicas comparten claves de ASP.NET Core Data Protection.

Esto permite que las cookies de autenticación sean validadas
por diferentes réplicas.

HttpContext.Session usa Redis compartido entre las tres réplicas; ver [H2](H2-REDIS.md).

## 8. Consideraciones de seguridad

- Los certificados mkcert son exclusivamente para desarrollo local.
- Producción requiere un certificado TLS apropiado.
- El archivo .env no debe subirse al repositorio.
- Las claves privadas de HTTPS deben mantenerse fuera de Git.
- Los puertos publicados están restringidos a 127.0.0.1.
- Se actualizó System.Security.Cryptography.Pkcs a 10.0.13
  para corregir advertencias de seguridad detectadas.
- La compilación y el arranque Docker con Aiven se registran, con su fecha y
  alcance, en [VALIDACION_NUBE_UNICA.md](VALIDACION_NUBE_UNICA.md).
