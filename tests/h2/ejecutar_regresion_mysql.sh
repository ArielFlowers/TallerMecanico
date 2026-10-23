#!/bin/sh
set -eu

# Solo se invoca en un SDK temporal, conectado a la red interna H2.
test -f /fuente/TallerMecanico.csproj
test -n "${H2_MYSQL_ROOT_PASSWORD:-}"
export TALLER_TEST_MYSQL="Server=mysql;Port=3306;User ID=root;Password=${H2_MYSQL_ROOT_PASSWORD};"
unset AUTOTALLER_PRUEBA_SMTP PLAYWRIGHT_MODULE TALLER_PDF_PYTHON
export DOTNET_CLI_HOME=/tmp/dotnet-h2
export NUGET_PACKAGES=/nuget
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

mkdir -p /tmp/h2-src
cp -R /fuente/. /tmp/h2-src/
cd /tmp/h2-src
proyecto=tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj
filtro='FullyQualifiedName!~RepositoriosMySqlTests&FullyQualifiedName!~ConfirmacionEmailMySqlTests&FullyQualifiedName!~LimiteReenvioMySqlTests&FullyQualifiedName!~RenovacionTokenMySqlTests&FullyQualifiedName!~RevisionSolicitudesMySqlTests&FullyQualifiedName!~EnvioGmailRealTests'
if test -n "${H2_FILTRO:-}"; then
    filtro="${filtro}&(${H2_FILTRO})"
fi
dotnet restore "$proyecto" --ignore-failed-sources -p:NuGetAudit=false --verbosity quiet
dotnet test "$proyecto" --no-restore \
    --filter "$filtro" \
    --logger 'console;verbosity=quiet' --logger 'trx;LogFileName=integracion-mysql.trx' \
    --results-directory /resultados -- RunConfiguration.TestSessionTimeout=600000 xUnit.ParallelizeTestCollections=false
