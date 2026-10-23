using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Globalization;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia;

public sealed class InicializadorCatalogoServicios(DatabaseConnectionFactory fabrica)
{
    public IReadOnlyList<string> Analizar()
    {
        using DbConnection conexion = fabrica.CreateConnection();
        conexion.Open();
        var existentes = LeerExistentes(conexion, null);
        var informe = existentes.Select(registro =>
            $"Id={registro.Servicio.Id}; Código={registro.Servicio.CodigoCatalogo ?? "sin catálogo"}; " +
            $"Nombre={registro.Servicio.Nombre}; Costo={registro.Servicio.Costo}; " +
            $"Descripción={registro.Servicio.Descripcion}; Horas={registro.Servicio.TiempoEstimadoHoras}; " +
            $"Unidad={registro.Servicio.UnidadCantidad ?? "fija"}; Órdenes={registro.Ordenes}").ToList();
        Preparar(existentes.Select(registro => registro.Servicio).ToArray(), [], informe);
        return informe;
    }

    public IReadOnlyList<string> Incorporar(IReadOnlyList<Servicio>? datosConfirmados = null)
    {
        using DbConnection conexion = fabrica.CreateConnection();
        conexion.Open();
        using DbTransaction transaccion = conexion.BeginTransaction();
        var existentes = LeerExistentes(conexion, transaccion).Select(registro => registro.Servicio).ToArray();
        var pendientes = new List<string>();
        var plan = Preparar(existentes, datosConfirmados ?? [], pendientes);
        if (pendientes.Count > 0) return pendientes;

        foreach (var (definicion, servicio) in plan)
            IncorporarServicio(conexion, transaccion, definicion, servicio);
        transaccion.Commit();
        return [];
    }

    private static List<(ServicioCatalogo Definicion, Servicio Servicio)> Preparar(
        IReadOnlyList<Servicio> existentes, IReadOnlyList<Servicio> confirmados, List<string> pendientes)
    {
        var plan = new List<(ServicioCatalogo Definicion, Servicio Servicio)>();
        if (confirmados.Any(servicio => CatalogoServicios.Obtener(servicio.CodigoCatalogo) is null) ||
            confirmados.Select(servicio => servicio.CodigoCatalogo).Distinct().Count() != confirmados.Count)
        {
            pendientes.Add("Los datos confirmados contienen códigos de catálogo desconocidos o repetidos.");
            return plan;
        }
        foreach (ServicioCatalogo definicion in CatalogoServicios.Servicios)
        {
            Servicio? confirmado = confirmados.SingleOrDefault(servicio => servicio.CodigoCatalogo == definicion.Codigo);
            Servicio? existente = existentes.SingleOrDefault(servicio => servicio.CodigoCatalogo == definicion.Codigo);
            var coincidencias = existentes.Where(servicio =>
                string.Equals(servicio.Nombre.Trim().Normalize(), definicion.Nombre.Normalize(), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (existente is null && confirmado is { Id: > 0 })
            {
                existente = existentes.SingleOrDefault(servicio => servicio.Id == confirmado.Id);
                if (existente is null)
                {
                    pendientes.Add($"{definicion.Codigo}: el Id confirmado no existe.");
                    continue;
                }
            }
            if (existente is null && coincidencias.Length == 1) existente = coincidencias[0];
            if (existente is null && coincidencias.Length > 1)
            {
                pendientes.Add($"{definicion.Codigo}: coincidencia ambigua o Id confirmado inexistente. Revisar los IDs manualmente.");
                continue;
            }
            if (existente is not null && existente.CodigoCatalogo is not null && existente.CodigoCatalogo != definicion.Codigo)
            {
                pendientes.Add($"{definicion.Codigo}: el Id {existente.Id} ya pertenece a otro código.");
                continue;
            }
            if (existente is null && confirmado is null)
            {
                string similares = string.Join(", ", existentes.Where(servicio => SonSimilares(servicio.Nombre, definicion.Nombre))
                    .Select(servicio => $"{servicio.Id}: {servicio.Nombre}"));
                pendientes.Add($"{definicion.Codigo}: faltan descripción y tiempo estimado confirmados. Revisar similares: {similares}.");
                continue;
            }
            Servicio datos = existente ?? confirmado!;
            var formulario = new ServicioFormViewModel
            {
                Nombre = definicion.Nombre, Descripcion = datos.Descripcion, Costo = definicion.PrecioInicial,
                TiempoEstimadoHoras = datos.TiempoEstimadoHoras, UnidadCantidad = definicion.UnidadCantidad
            };
            var errores = new List<ValidationResult>();
            bool valido = Validator.TryValidateObject(formulario, new ValidationContext(formulario), errores, true);
            if (!valido || new ValidacionServicios().Validar(formulario).Count > 0 || datos.TiempoEstimadoHoras > 9999.99m)
            {
                pendientes.Add($"{definicion.Codigo}: revisar descripción y tiempo estimado; deben respetar las validaciones actuales.");
                continue;
            }
            plan.Add((definicion, datos));
        }
        if (plan.Select(entrada => entrada.Servicio.Id).Where(id => id > 0).Distinct().Count() != plan.Count(entrada => entrada.Servicio.Id > 0))
            pendientes.Add("Un mismo Id no puede representar dos servicios del catálogo.");
        return plan;
    }

    private static bool SonSimilares(string nombre, string esperado)
    {
        CompareInfo comparacion = CultureInfo.GetCultureInfo("es").CompareInfo;
        const CompareOptions opciones = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return comparacion.IndexOf(nombre, esperado, opciones) >= 0 || comparacion.IndexOf(esperado, nombre, opciones) >= 0;
    }

    private static List<(Servicio Servicio, int Ordenes)> LeerExistentes(DbConnection conexion, DbTransaction? transaccion)
    {
        using DbCommand comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = """
            SELECT S.Id, S.CodigoCatalogo, S.Nombre, S.Descripcion, S.Costo, S.TiempoEstimadoHoras, S.UnidadCantidad,
                   (SELECT COUNT(DISTINCT O.Id) FROM Ordenes O
                    WHERE O.ServicioId = S.Id OR EXISTS (
                        SELECT 1 FROM DetalleServiciosOrdenes D WHERE D.OrdenId = O.Id AND D.ServicioId = S.Id))
            FROM Servicios S ORDER BY S.Id
            """ + (transaccion is null ? ";" : " FOR UPDATE;");
        using DbDataReader lector = comando.ExecuteReader();
        var registros = new List<(Servicio, int)>();
        while (lector.Read())
            registros.Add((new Servicio
            {
                Id = lector.GetInt32(0), CodigoCatalogo = lector.IsDBNull(1) ? null : lector.GetString(1),
                Nombre = lector.GetString(2), Descripcion = lector.GetString(3), Costo = lector.GetDecimal(4),
                TiempoEstimadoHoras = lector.GetDecimal(5), UnidadCantidad = lector.IsDBNull(6) ? null : lector.GetString(6)
            }, lector.GetInt32(7)));
        return registros;
    }

    private static void IncorporarServicio(DbConnection conexion, DbTransaction transaccion, ServicioCatalogo definicion, Servicio servicio)
    {
        bool incorporado = servicio.Id > 0 && servicio.CodigoCatalogo == definicion.Codigo;
        if (incorporado && servicio.UnidadCantidad == definicion.UnidadCantidad) return;
        using DbCommand comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = servicio.Id == 0
            ? """
                INSERT INTO Servicios (CodigoCatalogo, Nombre, Descripcion, Costo, TiempoEstimadoHoras, UnidadCantidad, CreadoPor)
                VALUES (@Codigo, @Nombre, @Descripcion, @Costo, @Horas, @Unidad, 'catalogo');
                """
            : incorporado
                ? "UPDATE Servicios SET UnidadCantidad = @Unidad, ModificadoPor = 'catalogo', FechaModificacion = UTC_TIMESTAMP() WHERE Id = @Id;"
                : """
                    UPDATE Servicios SET CodigoCatalogo = @Codigo, Nombre = @Nombre, Costo = @Costo, UnidadCantidad = @Unidad,
                        ModificadoPor = 'catalogo', FechaModificacion = UTC_TIMESTAMP() WHERE Id = @Id;
                    """;
        foreach (var (nombre, valor) in new (string, object?)[]
        {
            ("@Id", servicio.Id), ("@Codigo", definicion.Codigo), ("@Nombre", definicion.Nombre),
            ("@Descripcion", servicio.Descripcion), ("@Costo", definicion.PrecioInicial),
            ("@Horas", servicio.TiempoEstimadoHoras), ("@Unidad", definicion.UnidadCantidad)
        })
        {
            DbParameter parametro = comando.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value;
            comando.Parameters.Add(parametro);
        }
        comando.ExecuteNonQuery();
    }
}
