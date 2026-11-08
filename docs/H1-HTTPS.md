# HTTPS local con Kestrel

El despliegue SBA usa una sola instancia del Frontend y tres APIs independientes.
Kestrel atiende HTTPS directamente; ya no hay Nginx ni balanceo entre réplicas.
Hay dos tramos cifrados, cada uno con su certificado.

| Tramo | Certificado | Nombres |
|---|---|---|
| Navegador → Frontend | `certs/localhost.pem` y `certs/localhost-key.pem` | `localhost`, `127.0.0.1`, `::1` |
| Frontend → APIs | `certs/servicios.pem` y `certs/servicios-key.pem` | `servicio-ordenes`, `servicio-usuarios`, `servicio-reportes`, `localhost` |

`certs/ca-interna.pem` es la raíz pública de mkcert. Los comandos para generar los
cinco archivos están en [README](../README.md#preparar-windows-o-macos). La carpeta
está ignorada por Git y Docker, y cada equipo genera los suyos.

## Navegador → Frontend

| Variable | Valor |
|---|---|
| `ASPNETCORE_URLS` | `http://+:8080;https://+:8443` |
| `Kestrel__Certificates__Default__Path` | `/https/localhost.pem` |
| `Kestrel__Certificates__Default__KeyPath` | `/https/localhost-key.pem` |

El Frontend publica http://127.0.0.1 (puerto 80), http://127.0.0.1:8088 y
https://127.0.0.1 (puerto 443). Al abrir HTTPS, comprobar que el navegador confía
en el certificado del equipo.

## Frontend → APIs

| Variable | Dónde | Valor |
|---|---|---|
| `ASPNETCORE_URLS` | APIs | `https://+:8443`, sin HTTP plano |
| `Kestrel__Certificates__Default__Path` | APIs | `/https/servicios.pem` |
| `Kestrel__Certificates__Default__KeyPath` | APIs | `/https/servicios-key.pem` |
| `Servicios__OrdenesUrl`, `UsuariosUrl`, `ReportesUrl` | Frontend | `https://servicio-...:8443` |
| `Servicios__CertificadoCa` | Frontend | `/https/ca-interna.pem` |

`RegistroApis.CrearManejador` hace que el Frontend confíe solamente en esa CA y siga
validando el nombre del servidor. No se desactiva la validación del certificado.
`HttpsInternoTests` comprueba que se acepta la CA correcta y que se rechazan otra CA
y un nombre de servicio distinto.

La clave interna y los roles se siguen exigiendo: HTTPS protege el canal y la clave
identifica al Frontend.

Fuera de Docker (`dotnet run` y pruebas automatizadas) no se configura la CA y las
APIs usan HTTP en los puertos 5201 a 5203.

## Comprobación

Levantar las cuatro aplicaciones con `dotnet msbuild -t:NubeUp` y consultar su
estado con `dotnet msbuild -t:NubeStatus`. Las pruebas realizadas están en
[Validación](VALIDACION_SBA.md).

El volumen `taller-dpkeys` conserva las claves de cookies del Frontend. La sesión
y los borradores viven en su memoria y se descartan al reiniciarlo. Los datos
persistentes están en Aiven. `NubeDown` conserva contenedores y volúmenes.

mkcert es para desarrollo local. Consultar [SBA](SBA.md) para los límites y el orden
de arranque.
