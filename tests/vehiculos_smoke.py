"""Run after dotnet build: python tests/vehiculos_smoke.py (isolated SQLite databases)."""
import html
import http.cookiejar
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.parse
import urllib.request
from contextlib import closing, contextmanager

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "bin/Debug/net10.0/TallerMecanico.dll"


@contextmanager
def application(database):
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    environment = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development",
                       ASPNETCORE_URLS=base,
                       ConnectionStrings__DefaultConnection=f"Data Source={database}")
    with tempfile.TemporaryFile() as log:
        process = subprocess.Popen(["dotnet", str(DLL)], cwd=ROOT, env=environment,
                                   stdout=log, stderr=log)
        opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        try:
            for _ in range(100):
                try:
                    opener.open(base + "/Vehiculos", timeout=2).close()
                    break
                except OSError:
                    if process.poll() is not None:
                        log.seek(0)
                        raise AssertionError(log.read().decode())
                    time.sleep(0.1)
            else:
                raise AssertionError("Application did not start")
            yield base, opener
        finally:
            process.terminate()
            process.wait(timeout=10)


def get(client, path="/Vehiculos"):
    base, opener = client
    return html.unescape(opener.open(base + path).read().decode())


def post(client, handler="Create", **changes):
    page = get(client)
    token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)[1]
    data = {"Placa": "1234ABC", "Marca": "Toyota", "Modelo": "Hilux",
            "Kilometraje": "", "Observaciones": ""}
    data.update(changes)
    payload = {f"Formulario.{key}": value for key, value in data.items()}
    if handler == "Delete":
        payload["id"] = changes["Id"]
    payload["__RequestVerificationToken"] = token
    base, opener = client
    response = opener.open(base + "/Vehiculos?handler=" + handler,
                           urllib.parse.urlencode(payload).encode())
    return html.unescape(response.read().decode())


def rows(database):
    with closing(sqlite3.connect(database)) as connection:
        return connection.execute(
            "SELECT Id,Placa,Marca,Modelo,Kilometraje,Observaciones FROM Vehiculos ORDER BY Id").fetchall()


def run():
    with tempfile.TemporaryDirectory(prefix="vehiculos-tests-") as directory:
        database = Path(directory) / "new.db"
        with application(database) as client:
            post(client, Placa=" 1234 abc ", Marca=" To Yo Ta", Modelo=" hi LUX ",
                 Observaciones="  Revisar   frenos\nSegundo   detalle  ")
            saved = rows(database)
            assert saved == [(1, "1234ABC", "Toyota", "Hilux", 0,
                              "Revisar frenos\nSegundo detalle")], saved

            invalid_cases = [
                ({"Placa": "12ABC"}, "3 o 4 números y 3 letras"),
                ({"Placa": "123AB"}, "3 o 4 números y 3 letras"),
                ({"Placa": "12345ABC"}, "4 números y 3 letras"),
                ({"Placa": "1234ABCD"}, "4 números y 3 letras"),
                ({"Placa": "1234A-C"}, "4 números y 3 letras"),
                ({"Placa": "1234ABÑ"}, "4 números y 3 letras"),
                ({"Placa": "１２３４ABC"}, "4 números y 3 letras"),
                ({"Placa": ""}, "La placa es obligatoria"),
                ({"Placa": "1234abc"}, "ya está registrada"),
                ({"Marca": ""}, "Selecciona una marca válida"),
                ({"Marca": "Inventada"}, "Selecciona una marca válida"),
                ({"Modelo": ""}, "El modelo es obligatorio"),
                ({"Modelo": "Swift"}, "correspondiente a la marca"),
                ({"Modelo": "x" * 61}, "60 caracteres"),
                ({"Kilometraje": "-1"}, "no puede ser negativo"),
                ({"Kilometraje": "abc"}, "Formulario.Kilometraje"),
                ({"Kilometraje": "2147483648"}, "Formulario.Kilometraje"),
                ({"Observaciones": "x" * 251}, "250 caracteres"),
            ]
            for changes, message in invalid_cases:
                page = post(client, **changes)
                assert message in page, (changes, message)
                assert 'class="modal-overlay is-open"' in page
                assert rows(database) == saved, changes
            page = post(client, Placa="2222DEF", Kilometraje="-1")
            assert re.search(r'<option(?=[^>]*selected="selected")(?=[^>]*value="Toyota")[^>]*>', page)
            assert re.search(r'<option(?=[^>]*selected="selected")(?=[^>]*value="Hilux")[^>]*>', page)

            post(client, "Update", Id=1, Marca="  toYOta", Modelo="Land   Cruiser",
                 Kilometraje="2147483647", Observaciones="x" * 250)
            assert rows(database)[0][2:5] == ("Toyota", "Land Cruiser", 2147483647)
            post(client, Placa="2222DEF", Marca="Suzuki", Modelo="Swift")
            assert len(rows(database)) == 2
            before = rows(database)
            assert "ya está registrada" in post(client, "Update", Id=1, Placa="2222def")
            assert rows(database) == before
            assert "ya no existe" in post(client, "Update", Id=999, Placa="3333GHI")
            assert rows(database) == before
            assert "Land Cruiser" in get(client, "/Vehiculos?Buscar=Toyota")
            assert "Swift" not in get(client, "/Vehiculos?Buscar=Toyota").split('id="modal-crear"')[0]
            for path in ("/", "/Mecanicos", "/Servicios", "/Historial"):
                assert "<!DOCTYPE html>" in get(client, path)
            post(client, "Delete", Id=2)
            assert len(rows(database)) == 1
            post(client, Placa=" 123 abc ")
            assert rows(database)[1][1] == "123ABC"
            before = rows(database)
            assert "ya está registrada" in post(client, Placa="123abc")
            assert rows(database) == before
            post(client, "Update", Id=3, Placa="321xyz")
            assert rows(database)[1][1] == "321XYZ"
            post(client, "Delete", Id=3)
        preserved = rows(database)
        with application(database):
            assert rows(database) == preserved

        legacy = Path(directory) / "legacy.db"
        with closing(sqlite3.connect(legacy)) as connection:
            connection.executescript("""
                CREATE TABLE Vehiculos (Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Placa TEXT NOT NULL UNIQUE, Modelo TEXT NOT NULL,
                    Kilometraje INTEGER NOT NULL CHECK(Kilometraje>=0),
                    Observaciones TEXT NOT NULL DEFAULT '');
                INSERT INTO Vehiculos (Placa,Modelo,Kilometraje,Observaciones)
                VALUES ('123ABC','Toyota Hilux 2021',45000,'Conservar');
            """)
        for _ in range(2):
            with application(legacy) as client:
                assert rows(legacy) == [(1, "123ABC", "", "Toyota Hilux 2021", 45000, "Conservar")]
                assert "Pendiente" in get(client)
        with application(legacy) as client:
            page = post(client, "Update", Id=1, Placa="123ABC", Marca="", Modelo="Toyota Hilux 2021")
            assert "Modelo anterior:" in page and "Toyota Hilux 2021" in page
            post(client, "Update", Id=1, Placa="4321XYZ")
            assert rows(legacy)[0][1:4] == ("4321XYZ", "Toyota", "Hilux")
    print("PASS: normalization, validation, CRUD, search, pages, and new/legacy database restarts")


if __name__ == "__main__":
    run()
