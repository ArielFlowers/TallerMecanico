
# H1 - Docker, Nginx y HTTPS

## 1. Descripción

AutoTaller Pro utiliza Docker Compose para ejecutar tres réplicas
de una aplicación monolítica ASP.NET Core .NET 10, con MySQL
como base de datos y Nginx como balanceador de carga.

La arquitectura permite distribuir solicitudes entre las tres
réplicas y mantener la autenticación mediante cookies compartidas.

## 2. Arquitectura

- ASP.NET Core .NET 10
- MySQL 8.0
- Tres réplicas: app1, app2 y app3
- Nginx con balanceo Round Robin
- Data Protection con claves compartidas
- HTTP en puerto 80
- HTTPS en puerto 443
- Certificados locales generados mediante mkcert

## 3. Requisitos

- Docker Desktop
- .NET 10 SDK para desarrollo local
- mkcert para certificados HTTPS locales

## 4. Configuración de variables

Crear un archivo .env en la raíz del proyecto, utilizando
.env.example como referencia.

Variables necesarias:

- MYSQL_ROOT_PASSWORD
- MYSQL_PASSWORD

No subir el archivo .env al repositorio.

## 5. Generar certificados HTTPS

En macOS, instalar mkcert:

    brew install mkcert
    mkcert -install

Crear la carpeta para certificados:

    mkdir -p nginx/certs

Generar el certificado:

    mkcert -cert-file nginx/certs/localhost.pem -key-file nginx/certs/localhost-key.pem 127.0.0.1 localhost ::1

Los certificados son para desarrollo local.

La clave privada localhost-key.pem no debe subirse a GitHub.
Cada integrante debe generar sus propios certificados.

## 6. Ejecutar AutoTaller Pro

Validar la configuración:

    docker compose config -q

Construir y levantar los servicios:

    docker compose up -d --build

Consultar los contenedores:

    docker compose ps

## 7. Direcciones disponibles

- HTTP: http://127.0.0.1
- HTTPS: https://127.0.0.1
- HTTP alternativo: http://127.0.0.1:8088

Acceso directo a las réplicas:

- app1: http://127.0.0.1:5000
- app2: http://127.0.0.1:5001
- app3: http://127.0.0.1:5002

Estas direcciones corresponden al entorno local.

## 8. Prueba de balanceo de carga

Ejecutar:

    for i in {1..9}; do curl -fsS https://127.0.0.1/h1/replica; echo; done

Se esperan identificadores diferentes correspondientes a las
tres réplicas.

El dashboard también muestra la réplica activa.

## 9. Prueba de tolerancia a fallos

Detener la primera réplica:

    docker compose stop app1

Comprobar que la aplicación sigue respondiendo por HTTPS
a través de app2 y app3.

Restaurar la réplica:

    docker compose start app1

No utilizar docker compose down -v, porque elimina los
volúmenes persistentes, incluida la base de datos.

## 10. Autenticación compartida

Las tres réplicas comparten claves de ASP.NET Core Data Protection.

Esto permite que las cookies de autenticación sean validadas
por diferentes réplicas.

El almacenamiento de HttpContext.Session todavía es local
a cada instancia. Las sesiones distribuidas mediante Redis
corresponden a la historia H2.

## 11. Consideraciones de seguridad

- Los certificados mkcert son exclusivamente para desarrollo local.
- Producción requiere un certificado TLS apropiado.
- El archivo .env no debe subirse al repositorio.
- Las claves privadas de HTTPS deben mantenerse fuera de Git.
- Los puertos publicados están restringidos a 127.0.0.1.
- Se actualizó System.Security.Cryptography.Pkcs a 10.0.13
  para corregir advertencias de seguridad detectadas.
- La compilación .NET local pasó correctamente; la reconstrucción
  Docker posterior a esa actualización quedó pendiente de validar
  debido a las demoras de descarga de NuGet.
