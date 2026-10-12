-- AutoTaller Pro
-- Migracion 002: Control de reenvio de verificacion
--
-- Script histórico para versiones anteriores al inicializador actualizado.
-- El inicializador actual agrega esta columna únicamente si falta.
-- No ejecutar después de iniciar la aplicación actual: no es idempotente.
-- En versiones antiguas se aplicaba una sola vez después del script 001.
--
-- No elimina ni modifica solicitudes existentes.
-- Las solicitudes anteriores conservaran NULL.

ALTER TABLE SolicitudesRegistro
    ADD COLUMN UltimoIntentoVerificacionEn DATETIME(6) NULL;
