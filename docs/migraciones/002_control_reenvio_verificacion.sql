-- AutoTaller Pro
-- Migracion 002: Control de reenvio de verificacion
--
-- Ejecutar una sola vez despues de:
-- 001_registro_usuarios.sql
--
-- No elimina ni modifica solicitudes existentes.
-- Las solicitudes anteriores conservaran NULL.

ALTER TABLE SolicitudesRegistro
    ADD COLUMN UltimoIntentoVerificacionEn DATETIME(6) NULL;