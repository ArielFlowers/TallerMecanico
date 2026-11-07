# HTTPS local con Kestrel

El despliegue SBA usa una sola instancia del Frontend y tres APIs independientes.
Kestrel termina HTTPS directamente; ya no hay Nginx ni balanceo entre réplicas.
El nombre de esta guía se conserva para mantener los enlaces existentes.

## Certificados

Generar y confiar en los certificados mkcert de cada equipo según
[README](../README.md). Se guardan en `certs/localhost.pem` y
`certs/localhost-key.pem`; esta carpeta está ignorada por Git y Docker.
Si se conservan certificados del despliegue anterior en `nginx/certs/`, pueden
copiarse a `certs/` sin regenerarlos ni compartir la clave privada con el equipo.

Compose monta `./certs:/https:ro` solo en el Frontend y configura:

| Variable | Valor |
|---|---|
| `ASPNETCORE_URLS` | `http://+:8080;https://+:8443` |
| `Kestrel__Certificates__Default__Path` | `/https/localhost.pem` |
| `Kestrel__Certificates__Default__KeyPath` | `/https/localhost-key.pem` |

## Acceso y comprobación

Levantar las cuatro aplicaciones con `dotnet msbuild -t:NubeUp` y consultar su
estado con `dotnet msbuild -t:NubeStatus`. El Frontend publica:

- http://127.0.0.1 (puerto 80).
- http://127.0.0.1:8088.
- https://127.0.0.1 (puerto 443).

Abrir HTTPS en el navegador y comprobar que el certificado es del equipo y está
confiado. Los healthchecks de Compose usan HTTP interno en `/salud`: esa prueba
no verifica la confianza del navegador en mkcert. Las APIs no publican puertos
hacia la PC; el Frontend accede a ellas por la red interna de Compose.

El volumen `taller-dpkeys` conserva las claves de cookies del Frontend. La sesión
y los borradores viven en su memoria y se descartan al reiniciarlo. Los datos
persistentes están en Aiven. `NubeDown` conserva contenedores y volúmenes.

## Alcance

mkcert se usa para desarrollo local. El tráfico Frontend–APIs usa HTTP interno
con clave compartida y control de roles; cifrar ese tramo queda como mejora
pendiente. Consultar [SBA](SBA.md) para los límites y el orden de arranque.

Las pruebas históricas de balanceo y `/h1/replica` ya no aplican a este despliegue.
La validación del entorno anterior no constituye evidencia de arranque del SBA.
