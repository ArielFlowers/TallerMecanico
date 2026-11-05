# Conectar otra PC del equipo

Seguir la [guía principal de instalación y uso diario](../README.md). Allí están
los requisitos para Windows y macOS, certificados, las siete variables visibles
y los comandos .NET, que son iguales en ambos sistemas.
Cada PC construye su propia imagen; todos usan el código de la rama acordada y
la misma base `taller_mecanico` de Aiven MySQL 8.4.

## Qué entrega el responsable

- Acceso al repositorio y nombre de la rama acordada (actualmente `feature/bd-nube`).
- Por privado: host, puerto, usuario y clave de la base Aiven existente.
- Una cuenta activa de la aplicación, distinta de MySQL y Gmail.
- Cuenta SMTP de pruebas y contraseña de aplicación, o indicación de usar una propia.

El compañero configura `.env` y ejecuta `dotnet msbuild -t:NubeUp`. No necesita
crear otro Aiven, importar dumps ni ejecutar la prueba del trigger.
Los scripts administrativos también leen `.env`; no se necesita otro archivo
con las mismas credenciales.

## Comprobación entre compañeros

Con cuentas autorizadas, crear un cliente ficticio válido en una PC y encontrarlo
en la otra. Apagar el stack web de una PC y comprobar que la otra sigue accediendo
a Aiven. Coordinar estos cambios con el equipo. Compartir MySQL no comparte las
cookies entre PCs: cada integrante mantiene sus claves Data Protection y su sesión.

La migración y recuperación corresponden al [responsable de la base](BD_NUBE.md).
El antiguo anfitrión/Tailscale no interviene en este flujo.
