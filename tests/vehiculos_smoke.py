"""Ejecuta las pruebas de vehículos xUnit; MySQL y navegador son optativos."""
from pathlib import Path
import subprocess
import sys

raiz = Path(__file__).resolve().parent.parent
resultado = subprocess.run(
    ["dotnet", "test", "tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj",
     "--nologo", "--filter", "FullyQualifiedName~Vehiculos"],
    cwd=raiz,
)
sys.exit(resultado.returncode)
