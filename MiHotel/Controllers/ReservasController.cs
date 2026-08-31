using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Services;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class ReservasController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly DisponibilidadService _disponibilidadService;
        private readonly FacturacionService _facturacionService;
        private readonly IDataProtector _protectorFlujoReservaCliente;
        private const int RegistrosPorPagina = 20;

        public ReservasController(
            ConexionBD conexionBD,
            DisponibilidadService disponibilidadService,
            FacturacionService facturacionService,
            IDataProtectionProvider dataProtectionProvider)
        {
            _conexionBD = conexionBD;
            _disponibilidadService = disponibilidadService;
            _facturacionService = facturacionService;
            _protectorFlujoReservaCliente = dataProtectionProvider.CreateProtector("MiHotel.Reservas.CrearCliente");
        }

        // ============================================================
        // SESION Y ROL
        // ============================================================

        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrWhiteSpace(idUsuario);
        }

        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            return null;
        }

        private string ObtenerNombreRolSesion()
        {
            return HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
        }

        private bool EsClienteSesion()
        {
            return ObtenerNombreRolSesion() == "cliente";
        }

        private bool EsAdministradorSesion()
        {
            return ObtenerNombreRolSesion() == "admin";
        }

        private IActionResult? ValidarAccesoSoloAdministrativo()
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            string rol = ObtenerNombreRolSesion();

            if (rol != "admin" && rol != "recepcionista")
            {
                TempData["Mensaje"] = "No tiene acceso a esa opción.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        private IActionResult? ValidarAccesoSoloCliente()
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            if (!EsClienteSesion())
            {
                TempData["Mensaje"] = "No tiene acceso a esa opción.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        private int ObtenerIdUsuarioSesion()
        {
            string? idUsuarioSesion = HttpContext.Session.GetString("IdUsuario");

            if (!int.TryParse(idUsuarioSesion, out int idUsuario))
            {
                throw new Exception("No se pudo identificar el usuario de la sesión.");
            }

            return idUsuario;
        }

        private int ObtenerIdClienteSesion()
        {
            if (!EsClienteSesion())
            {
                throw new Exception("La sesión actual no corresponde a un cliente.");
            }

            return ObtenerIdUsuarioSesion();
        }

        private string ObtenerNombreUsuarioSesion()
        {
            return HttpContext.Session.GetString("NombreUsuario")?.Trim() ?? "";
        }

        // ============================================================
        // CATALOGOS Y HELPERS DE BD
        // ============================================================

        private int ObtenerIdTipoCliente(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_tipoclipro
                FROM tipo_clipro
                WHERE LOWER(tipo) = 'cliente'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe el tipo 'cliente' en tipo_clipro.");
            }

            return Convert.ToInt32(resultado);
        }

        private int ObtenerIdTipoProserHabitacion(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_tipoproser
                FROM tipo_proser
                WHERE LOWER(nombre) = 'habitacion'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe el tipo 'habitacion' en tipo_proser.");
            }

            return Convert.ToInt32(resultado);
        }

        private int ObtenerIdTipoMovimiento(MySqlConnection conexion, string nombreTipo)
        {
            string consulta = @"
                SELECT id_tipomov
                FROM tipo_movimiento
                WHERE LOWER(nombre_tipomov) = @nombre
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@nombre", nombreTipo.Trim().ToLower());

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception($"No existe el tipo de movimiento '{nombreTipo}'.");
            }

            return Convert.ToInt32(resultado);
        }

        private int ObtenerIdFormaPago(MySqlConnection conexion, string nombreFormaPago)
        {
            string consulta = @"
                SELECT id_formapago
                FROM forma_pago
                WHERE LOWER(nombre_forma) = @nombre
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@nombre", nombreFormaPago.Trim().ToLower());

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception($"No existe la forma de pago '{nombreFormaPago}'.");
            }

            return Convert.ToInt32(resultado);
        }

        private int ObtenerIdEstadoHabitacion(MySqlConnection conexion, string nombre)
        {
            string query = @"
                SELECT id_tipoestado
                FROM tipo_estado
                WHERE LOWER(estado) = @nombre
                LIMIT 1;";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre.ToLower());

            object? result = cmd.ExecuteScalar();

            if (result == null)
            {
                throw new Exception($"Estado '{nombre}' no existe.");
            }

            return Convert.ToInt32(result);
        }

        private string ObtenerColumnaOrden(string columna)
        {
            return columna.Trim().ToLower() switch
            {
                "cliente" => "c.nombre",
                "empresa" => "ec.nombre",
                "fecha_entrada" => "r.fecha_entrada",
                "hora_checkin" => "r.fecha_hora_checkin",
                "hora_checkout" => "r.fecha_hora_checkout",
                _ => "r.fecha_entrada"
            };
        }

        // ============================================================
        // VALIDACIONES Y UTILIDADES DE RESERVA
        // ============================================================

        private bool FechasReservaSonValidas(DateTime fechaEntrada, DateTime fechaSalida, out string mensaje)
        {
            if (fechaEntrada.Date < DateTime.Today)
            {
                mensaje = "La fecha de entrada no puede ser menor a hoy.";
                return false;
            }

            if (fechaSalida.Date <= fechaEntrada.Date)
            {
                mensaje = "La fecha de salida debe ser mayor que la fecha de entrada.";
                return false;
            }

            mensaje = string.Empty;
            return true;
        }

        private List<DateTime> NormalizarFechasSeparadas(IEnumerable<DateTime>? fechas)
        {
            return (fechas ?? Enumerable.Empty<DateTime>())
                .Where(fecha => fecha != DateTime.MinValue)
                .Select(fecha => fecha.Date)
                .Distinct()
                .OrderBy(fecha => fecha)
                .ToList();
        }

        private bool FechasSeparadasSonValidas(IEnumerable<DateTime>? fechas, out List<DateTime> fechasNormalizadas, out string mensaje)
        {
            List<DateTime> fechasIngresadas = (fechas ?? Enumerable.Empty<DateTime>())
                .Where(fecha => fecha != DateTime.MinValue)
                .Select(fecha => fecha.Date)
                .ToList();

            fechasNormalizadas = NormalizarFechasSeparadas(fechasIngresadas);

            if (fechasIngresadas.Count != fechasNormalizadas.Count)
            {
                mensaje = "No puede seleccionar la misma fecha más de una vez.";
                return false;
            }

            if (fechasNormalizadas.Count < 2)
            {
                mensaje = "Seleccione al menos dos fechas para crear una reserva agrupada.";
                return false;
            }

            if (fechasNormalizadas.Count > 31)
            {
                mensaje = "Una reserva agrupada no puede contener más de 31 fechas.";
                return false;
            }

            if (fechasNormalizadas.Any(fecha => fecha < DateTime.Today))
            {
                mensaje = "Las fechas de hospedaje no pueden ser menores a hoy.";
                return false;
            }

            for (int i = 1; i < fechasNormalizadas.Count; i++)
            {
                int diferenciaDias = (fechasNormalizadas[i] - fechasNormalizadas[i - 1]).Days;

                if (diferenciaDias > 3)
                {
                    mensaje = "Entre dos fechas del mismo grupo solo puede haber uno o dos días intermedios.";
                    return false;
                }
            }

            mensaje = string.Empty;
            return true;
        }

        private bool ClienteTieneFlujoCrearValido(ReservaFormViewModel modelo, out string mensaje)
        {
            if (modelo.IdHabitacion <= 0)
            {
                mensaje = "Debe seleccionar primero una habitación disponible.";
                return false;
            }

            if (!FechasReservaSonValidas(modelo.FechaEntrada, modelo.FechaSalida, out mensaje))
            {
                return false;
            }

            mensaje = string.Empty;
            return true;
        }

        private string GenerarTokenFlujoCliente(int idHabitacion, DateTime fechaEntrada, DateTime fechaSalida)
        {
            string contenido = $"{idHabitacion}|{fechaEntrada:yyyy-MM-dd}|{fechaSalida:yyyy-MM-dd}";
            return _protectorFlujoReservaCliente.Protect(contenido);
        }

        private bool TokenFlujoClienteEsValido(
            string? tokenFlujoCliente,
            int idHabitacion,
            DateTime fechaEntrada,
            DateTime fechaSalida)
        {
            if (string.IsNullOrWhiteSpace(tokenFlujoCliente))
            {
                return false;
            }

            try
            {
                string contenidoEsperado = $"{idHabitacion}|{fechaEntrada:yyyy-MM-dd}|{fechaSalida:yyyy-MM-dd}";
                string contenidoReal = _protectorFlujoReservaCliente.Unprotect(tokenFlujoCliente);

                return string.Equals(contenidoEsperado, contenidoReal, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private bool HabitacionSigueDisponibleParaCrear(int idHabitacion, DateTime fechaEntrada, DateTime fechaSalida)
        {
            return _disponibilidadService.EstaHabitacionDisponible(
                idHabitacion,
                fechaEntrada,
                fechaSalida
            );
        }

        private void ConfigurarVistaCrear(
            ReservaFormViewModel modelo,
            bool esCliente,
            string? tokenFlujoCliente = null)
        {
            if (esCliente)
            {
                ViewBag.NombreClienteSesion = ObtenerNombreUsuarioSesion();
            }

            ViewBag.EsClienteSesion = esCliente;
            ViewBag.BloquearHabitacion = esCliente && modelo.IdHabitacion > 0;
            ViewBag.BloquearFechas = esCliente;

            if (!string.IsNullOrWhiteSpace(tokenFlujoCliente))
            {
                ViewBag.TokenFlujoCliente = tokenFlujoCliente;
            }
        }

        private ReservaDetalleViewModel? ObtenerReservaDetallePorId(MySqlConnection conexion, int idReserva, int? idClipro = null)
        {
            string filtroCliente = idClipro.HasValue ? "AND r.id_clipro = @id_clipro" : "";

            string consulta = $@"
                SELECT
                    r.id_reserva,
                    r.id_reserva_grupo,
                    r.id_clipro,
                    c.nombre AS cliente,
                    c.nit AS nit_cliente,
                    ec.nombre AS empresa_procedencia,
                    p.codigo AS habitacion,
                    r.fecha_entrada,
                    r.fecha_salida,
                    r.fecha_hora_checkin,
                    r.fecha_hora_checkout,
                    r.codigo_seguridad,
                    r.cantidad_personas,
                    r.total_reserva,
                    r.saldo_pendiente,
                    r.estado,
                    r.observaciones
                FROM reserva r
                INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                LEFT JOIN cliente_detalle cd ON c.id_clipro = cd.id_clipro
                LEFT JOIN empresa_cliente ec ON cd.id_empresa_cliente = ec.id_empresa_cliente
                INNER JOIN proser p ON r.id_habitacion = p.id_proser
                WHERE r.id_reserva = @id
                {filtroCliente}
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id", idReserva);

            if (idClipro.HasValue)
            {
                comando.Parameters.AddWithValue("@id_clipro", idClipro.Value);
            }

            using var lector = comando.ExecuteReader();

            if (!lector.Read())
            {
                return null;
            }

            return new ReservaDetalleViewModel
            {
                IdReserva = Convert.ToInt32(lector["id_reserva"]),
                IdReservaGrupo = lector["id_reserva_grupo"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(lector["id_reserva_grupo"]),
                Cliente = lector["cliente"]?.ToString() ?? "",
                NitCliente = lector["nit_cliente"] == DBNull.Value ? null : lector["nit_cliente"].ToString(),
                EmpresaProcedencia = lector["empresa_procedencia"] == DBNull.Value
                    ? null
                    : lector["empresa_procedencia"]?.ToString(),
                Habitacion = lector["habitacion"]?.ToString() ?? "",
                FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                FechaHoraCheckIn = lector["fecha_hora_checkin"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(lector["fecha_hora_checkin"]),
                FechaHoraCheckOut = lector["fecha_hora_checkout"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(lector["fecha_hora_checkout"]),
                CantidadPersonas = Convert.ToInt32(lector["cantidad_personas"]),
                TotalReserva = Convert.ToDecimal(lector["total_reserva"]),
                SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]),
                Estado = lector["estado"]?.ToString() ?? "",
                Observaciones = lector["observaciones"] == DBNull.Value
                    ? null
                    : lector["observaciones"]?.ToString(),

                    CodigoSeguridad = lector["codigo_seguridad"] == DBNull.Value
                    ? null
                    : lector["codigo_seguridad"]?.ToString(),
            };
        }

        private List<ReservaGrupoItemViewModel> ObtenerReservasDelGrupo(MySqlConnection conexion, int idReservaGrupo)
        {
            const string consulta = @"
                SELECT
                    r.id_reserva,
                    p.codigo AS habitacion,
                    r.fecha_entrada,
                    r.fecha_salida,
                    r.total_reserva,
                    r.saldo_pendiente,
                    r.estado
                FROM reserva r
                INNER JOIN proser p ON r.id_habitacion = p.id_proser
                WHERE r.id_reserva_grupo = @id_reserva_grupo
                ORDER BY r.fecha_entrada, r.id_reserva;";

            var reservas = new List<ReservaGrupoItemViewModel>();

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                reservas.Add(new ReservaGrupoItemViewModel
                {
                    IdReserva = Convert.ToInt32(lector["id_reserva"]),
                    Habitacion = lector["habitacion"]?.ToString() ?? "",
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    TotalReserva = Convert.ToDecimal(lector["total_reserva"]),
                    SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]),
                    Estado = lector["estado"]?.ToString() ?? ""
                });
            }

            return reservas;
        }

        private sealed class DestinoFacturaReserva
        {
            public int IdReserva { get; init; }
            public int? IdReservaGrupo { get; init; }
        }

        private DestinoFacturaReserva? ObtenerDestinoFactura(
            MySqlConnection conexion,
            int idReserva,
            MySqlTransaction? transaccion = null,
            bool bloquear = false)
        {
            string consulta = $@"
                SELECT id_reserva, id_reserva_grupo
                FROM reserva
                WHERE id_reserva = @id_reserva
                LIMIT 1{(bloquear ? " FOR UPDATE" : "")};";

            int? idReservaGrupo;

            using (var comando = new MySqlCommand(consulta, conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    return null;
                }

                idReservaGrupo = lector["id_reserva_grupo"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(lector["id_reserva_grupo"]);
            }

            if (bloquear && idReservaGrupo.HasValue)
            {
                using var comandoGrupo = new MySqlCommand(@"
                    SELECT id_reserva_grupo
                    FROM reserva_grupo
                    WHERE id_reserva_grupo = @id_reserva_grupo
                    FOR UPDATE;", conexion, transaccion);
                comandoGrupo.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo.Value);

                if (comandoGrupo.ExecuteScalar() == null)
                {
                    return null;
                }
            }

            return new DestinoFacturaReserva
            {
                IdReserva = idReserva,
                IdReservaGrupo = idReservaGrupo
            };
        }

        private void CargarDatosFacturacion(MySqlConnection conexion, ReservaDetalleViewModel modelo)
        {
            modelo.EsAdministrador = EsAdministradorSesion();

            using (var comando = new MySqlCommand(@"
                SELECT requiere_factura, estado_facturacion, estado_administrativo
                FROM reserva_facturacion
                WHERE id_reserva = @id_reserva
                LIMIT 1;", conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);
                using var lector = comando.ExecuteReader();
                if (lector.Read())
                {
                    modelo.RequiereFactura = lector["requiere_factura"] == DBNull.Value
                        ? null
                        : Convert.ToBoolean(lector["requiere_factura"]);
                    modelo.EstadoFacturacion = lector["estado_facturacion"]?.ToString() ?? "sin_definir";
                    modelo.EstadoAdministrativo = lector["estado_administrativo"]?.ToString() ?? "pendiente_revision";
                }
            }

            using var comandoDocumentos = new MySqlCommand(@"
                SELECT df.id_documento_fiscal, df.tipo_documento, df.nit_receptor,
                       df.serie, df.numero_dte, df.nombre_original, df.tamano,
                       df.estado, df.fecha_registro, df.motivo_estado,
                       u.nombre_usuario
                FROM documento_fiscal_reserva dfr
                INNER JOIN documento_fiscal df
                    ON df.id_documento_fiscal = dfr.id_documento_fiscal
                INNER JOIN usuario u
                    ON u.id_usuario = df.id_usuario_registro
                WHERE dfr.id_reserva = @id_reserva
                ORDER BY df.fecha_registro DESC, df.id_documento_fiscal DESC;", conexion);
            comandoDocumentos.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);
            using var lectorDocumentos = comandoDocumentos.ExecuteReader();

            while (lectorDocumentos.Read())
            {
                modelo.DocumentosFiscales.Add(new DocumentoFiscalViewModel
                {
                    IdDocumentoFiscal = Convert.ToInt64(lectorDocumentos["id_documento_fiscal"]),
                    TipoDocumento = lectorDocumentos["tipo_documento"]?.ToString() ?? "factura",
                    NitReceptor = lectorDocumentos["nit_receptor"] == DBNull.Value ? null : lectorDocumentos["nit_receptor"].ToString(),
                    Serie = lectorDocumentos["serie"] == DBNull.Value ? null : lectorDocumentos["serie"].ToString(),
                    NumeroDte = lectorDocumentos["numero_dte"] == DBNull.Value ? null : lectorDocumentos["numero_dte"].ToString(),
                    NombreOriginal = lectorDocumentos["nombre_original"]?.ToString() ?? "factura.pdf",
                    Tamano = Convert.ToInt64(lectorDocumentos["tamano"]),
                    Estado = lectorDocumentos["estado"]?.ToString() ?? "vigente",
                    FechaRegistro = Convert.ToDateTime(lectorDocumentos["fecha_registro"]),
                    MotivoEstado = lectorDocumentos["motivo_estado"] == DBNull.Value ? null : lectorDocumentos["motivo_estado"].ToString(),
                    UsuarioRegistro = lectorDocumentos["nombre_usuario"]?.ToString() ?? ""
                });
            }

            lectorDocumentos.Close();

            if (modelo.EsAdministrador)
            {
                using var comandoHistorial = new MySqlCommand(@"
                    SELECT h.id_historial, h.accion, h.estado_anterior, h.estado_nuevo,
                           h.detalle, h.fecha_hora, u.nombre_usuario
                    FROM reserva_facturacion_historial h
                    INNER JOIN usuario u ON u.id_usuario = h.id_usuario
                    WHERE h.id_reserva = @id_reserva
                    ORDER BY h.fecha_hora DESC, h.id_historial DESC;", conexion);
                comandoHistorial.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);
                using var lectorHistorial = comandoHistorial.ExecuteReader();

                while (lectorHistorial.Read())
                {
                    modelo.HistorialFacturacion.Add(new FacturacionHistorialViewModel
                    {
                        IdHistorial = Convert.ToInt64(lectorHistorial["id_historial"]),
                        Accion = lectorHistorial["accion"]?.ToString() ?? "",
                        EstadoAnterior = lectorHistorial["estado_anterior"] == DBNull.Value
                            ? null
                            : lectorHistorial["estado_anterior"].ToString(),
                        EstadoNuevo = lectorHistorial["estado_nuevo"]?.ToString() ?? "",
                        Detalle = lectorHistorial["detalle"] == DBNull.Value
                            ? null
                            : lectorHistorial["detalle"].ToString(),
                        Usuario = lectorHistorial["nombre_usuario"]?.ToString() ?? "",
                        FechaHora = Convert.ToDateTime(lectorHistorial["fecha_hora"])
                    });
                }
            }
        }

        private int ObtenerIdUsuarioValidoParaMovimiento(MySqlConnection conexion)
        {
            int idSesion = ObtenerIdUsuarioSesion();

            string consultaExiste = @"
                SELECT COUNT(*)
                FROM usuario
                WHERE id_usuario = @id_usuario;";

            using (var comandoExiste = new MySqlCommand(consultaExiste, conexion))
            {
                comandoExiste.Parameters.AddWithValue("@id_usuario", idSesion);
                int existe = Convert.ToInt32(comandoExiste.ExecuteScalar());

                if (existe > 0)
                {
                    return idSesion;
                }
            }

            string consultaPrimero = @"
                SELECT id_usuario
                FROM usuario
                ORDER BY id_usuario
                LIMIT 1;";

            using var comandoPrimero = new MySqlCommand(consultaPrimero, conexion);
            object? resultado = comandoPrimero.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe ningún usuario válido en la tabla usuario para registrar movimientos.");
            }

            return Convert.ToInt32(resultado);
        }


        //
        // METODO DE VALIDACIÓN DE RESERVAS.
        //

        [HttpGet]
        public IActionResult BuscarPorCodigo()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BuscarPorCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                ViewBag.Mensaje = "Debe ingresar un código de seguridad.";
                return View();
            }

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string query = @"
        SELECT id_reserva
        FROM reserva
        WHERE codigo_seguridad = @codigo
        LIMIT 1;";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@codigo", codigo.Trim());

            var resultado = cmd.ExecuteScalar();

            if (resultado == null)
            {
                ViewBag.Mensaje = "No se encontró una reserva con ese código.";
                return View();
            }

            int idReserva = Convert.ToInt32(resultado);

            // reutiliza tu método existente.
            return RedirectToAction("Detalle", new { id = idReserva });
        }

        // ============================================================
        // PRECIO HISTORICO DE HABITACION
        // ============================================================

        private decimal ObtenerPrecioHabitacion(MySqlConnection conexion, int idHabitacion)
        {
            string consulta = @"
            SELECT s.precio
            FROM proser p
            INNER JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
            WHERE p.id_proser = @id_habitacion
            LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_habitacion", idHabitacion);

            object? resultado = comando.ExecuteScalar();

            if (resultado == null || resultado == DBNull.Value)
            {
                throw new Exception("No se pudo obtener el precio de la subcategoría de la habitación seleccionada.");
            }

            return Convert.ToDecimal(resultado);
        }

        // ============================================================
        // CARGA DE COMBOS
        // ============================================================

        private void CargarCombos(
            DateTime? fechaEntrada = null,
            DateTime? fechaSalida = null,
            int? idHabitacionSeleccionada = null)
        {
            List<dynamic> clientes = new List<dynamic>();
            List<dynamic> habitaciones = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);
                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string consultaClientes = @"
                    SELECT id_clipro, nombre
                    FROM clipro
                    WHERE id_tipoclipro = @id_tipoclipro
                      AND estado = 'activo'
                    ORDER BY nombre;";

                using (var comandoClientes = new MySqlCommand(consultaClientes, conexion))
                {
                    comandoClientes.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                    using var lectorClientes = comandoClientes.ExecuteReader();

                    while (lectorClientes.Read())
                    {
                        clientes.Add(new
                        {
                            Id = Convert.ToInt32(lectorClientes["id_clipro"]),
                            Nombre = lectorClientes["nombre"]?.ToString() ?? ""
                        });
                    }
                }

                if (fechaEntrada.HasValue && fechaSalida.HasValue && fechaSalida.Value.Date > fechaEntrada.Value.Date)
                {
                    var habitacionesDisponibles = _disponibilidadService.ObtenerHabitacionesDisponibles(
                        fechaEntrada.Value,
                        fechaSalida.Value
                    );

                    foreach (var habitacion in habitacionesDisponibles)
                    {
                        habitaciones.Add(new
                        {
                            Id = habitacion.IdHabitacion,
                            Codigo = habitacion.NumeroHabitacion,
                            Precio = habitacion.Precio,
                            Tipo = habitacion.TipoHabitacion
                        });
                    }

                    if (idHabitacionSeleccionada.HasValue &&
                        !habitaciones.Any(h => h.Id == idHabitacionSeleccionada.Value))
                    {
                        string consultaHabitacionSeleccionada = @"
                            SELECT
                                p.id_proser,
                                p.codigo,
                                p.precio,
                                COALESCE(s.nombre_subcategoria, '-') AS tipo_habitacion
                            FROM proser p
                            LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                            WHERE p.id_proser = @id_habitacion
                              AND p.id_tipoproser = @id_tipoproser
                            LIMIT 1;";

                        using var comandoHabitacionSeleccionada = new MySqlCommand(consultaHabitacionSeleccionada, conexion);
                        comandoHabitacionSeleccionada.Parameters.AddWithValue("@id_habitacion", idHabitacionSeleccionada.Value);
                        comandoHabitacionSeleccionada.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                        using var lectorHabitacionSeleccionada = comandoHabitacionSeleccionada.ExecuteReader();

                        if (lectorHabitacionSeleccionada.Read())
                        {
                            habitaciones.Add(new
                            {
                                Id = Convert.ToInt32(lectorHabitacionSeleccionada["id_proser"]),
                                Codigo = lectorHabitacionSeleccionada["codigo"]?.ToString() ?? "",
                                Precio = Convert.ToDecimal(lectorHabitacionSeleccionada["precio"]),
                                Tipo = lectorHabitacionSeleccionada["tipo_habitacion"]?.ToString() ?? "-"
                            });
                        }
                    }
                }
                else
                {
                    string consultaHabitaciones = @"
                        SELECT 
                            p.id_proser,
                            p.codigo,
                            p.precio,
                            COALESCE(s.nombre_subcategoria, '-') AS tipo_habitacion
                        FROM proser p
                        LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                        WHERE p.id_tipoproser = @id_tipoproser
                        ORDER BY p.codigo;";

                    using var comandoHabitaciones = new MySqlCommand(consultaHabitaciones, conexion);
                    comandoHabitaciones.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                    using var lectorHabitaciones = comandoHabitaciones.ExecuteReader();

                    while (lectorHabitaciones.Read())
                    {
                        habitaciones.Add(new
                        {
                            Id = Convert.ToInt32(lectorHabitaciones["id_proser"]),
                            Codigo = lectorHabitaciones["codigo"]?.ToString() ?? "",
                            Precio = Convert.ToDecimal(lectorHabitaciones["precio"]),
                            Tipo = lectorHabitaciones["tipo_habitacion"]?.ToString() ?? "-"
                        });
                    }
                }
            }
            catch
            {
            }

            ViewBag.Clientes = clientes;
            ViewBag.Habitaciones = habitaciones;
        }

        // ============================================================
        // MOVIMIENTOS Y PAGOS
        // ============================================================

        private decimal ObtenerTotalPagadoReserva(MySqlConnection conexion, int idReserva)
        {
            string consulta = @"
                SELECT COALESCE(SUM(pago.monto), 0)
                FROM
                (
                    SELECT COALESCE(SUM(d.subtotal), 0) AS monto
                    FROM movimiento m
                    INNER JOIN detalle d ON m.id_movimiento = d.id_movimiento
                    INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                    WHERE m.id_reserva = @id_reserva
                      AND m.estado = 'activo'
                      AND LOWER(tm.nombre_tipomov) IN ('reserva', 'abono')

                    UNION ALL

                    SELECT COALESCE(SUM(a.monto), 0) AS monto
                    FROM movimiento_reserva_aplicacion a
                    INNER JOIN movimiento m ON a.id_movimiento = m.id_movimiento
                    INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                    WHERE a.id_reserva = @id_reserva
                      AND m.estado = 'activo'
                      AND LOWER(tm.nombre_tipomov) = 'abono'
                ) pago;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_reserva", idReserva);

            object? resultado = comando.ExecuteScalar();
            return resultado == null || resultado == DBNull.Value ? 0 : Convert.ToDecimal(resultado);
        }

        private void RegistrarMovimientoReserva(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idReserva,
            int idUsuario,
            int idClipro,
            int idHabitacion,
            int idFormaPago,
            int idTipoMovimiento,
            decimal monto,
            string descripcionDetalle,
            string? observaciones)
        {
            string insertarMovimiento = @"
                INSERT INTO movimiento
                (
                    id_usuario,
                    id_clipro,
                    id_tipomov,
                    id_formapago,
                    id_reserva,
                    estado,
                    observaciones
                )
                VALUES
                (
                    @id_usuario,
                    @id_clipro,
                    @id_tipomov,
                    @id_formapago,
                    @id_reserva,
                    'activo',
                    @observaciones
                );";

            int idMovimientoGenerado;

            using (var comandoMovimiento = new MySqlCommand(insertarMovimiento, conexion, transaccion))
            {
                comandoMovimiento.Parameters.AddWithValue("@id_usuario", idUsuario);
                comandoMovimiento.Parameters.AddWithValue("@id_clipro", idClipro);
                comandoMovimiento.Parameters.AddWithValue("@id_tipomov", idTipoMovimiento);
                comandoMovimiento.Parameters.AddWithValue("@id_formapago", idFormaPago);
                comandoMovimiento.Parameters.AddWithValue("@id_reserva", idReserva);
                comandoMovimiento.Parameters.AddWithValue("@observaciones",
                    string.IsNullOrWhiteSpace(observaciones) ? DBNull.Value : observaciones.Trim());

                comandoMovimiento.ExecuteNonQuery();
                idMovimientoGenerado = Convert.ToInt32(comandoMovimiento.LastInsertedId);
            }

            string insertarDetalle = @"
                INSERT INTO detalle
                (
                    id_movimiento,
                    id_proser,
                    cantidad,
                    precio_unitario,
                    subtotal,
                    descripcion
                )
                VALUES
                (
                    @id_movimiento,
                    @id_proser,
                    1,
                    @precio_unitario,
                    @subtotal,
                    @descripcion
                );";

            using var comandoDetalle = new MySqlCommand(insertarDetalle, conexion, transaccion);
            comandoDetalle.Parameters.AddWithValue("@id_movimiento", idMovimientoGenerado);
            comandoDetalle.Parameters.AddWithValue("@id_proser", idHabitacion);
            comandoDetalle.Parameters.AddWithValue("@precio_unitario", monto);
            comandoDetalle.Parameters.AddWithValue("@subtotal", monto);
            comandoDetalle.Parameters.AddWithValue("@descripcion", descripcionDetalle);

            comandoDetalle.ExecuteNonQuery();
        }

        private int InsertarReservaConCuenta(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            ReservaFormViewModel modelo,
            DateTime fechaEntrada,
            DateTime fechaSalida,
            decimal precioPorNoche,
            int? idReservaGrupo,
            int idUsuario,
            int idTipoMovimientoCxc,
            int idFormaPagoCredito,
            bool registrarCuentaIndividual = true)
        {
            int noches = (fechaSalida.Date - fechaEntrada.Date).Days;
            decimal totalReserva = precioPorNoche * noches * modelo.CantidadPersonas;

            const string insertarReserva = @"
                INSERT INTO reserva
                (
                    id_reserva_grupo,
                    id_clipro,
                    id_habitacion,
                    precio_noche_aplicado,
                    fecha_entrada,
                    fecha_salida,
                    cantidad_personas,
                    total_reserva,
                    saldo_pendiente,
                    estado,
                    observaciones
                )
                VALUES
                (
                    @id_reserva_grupo,
                    @id_clipro,
                    @id_habitacion,
                    @precio_noche_aplicado,
                    @fecha_entrada,
                    @fecha_salida,
                    @cantidad_personas,
                    @total_reserva,
                    @saldo_pendiente,
                    'pendiente',
                    @observaciones
                );";

            int idReserva;

            using (var comando = new MySqlCommand(insertarReserva, conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo.HasValue ? idReservaGrupo.Value : DBNull.Value);
                comando.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                comando.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                comando.Parameters.AddWithValue("@precio_noche_aplicado", precioPorNoche);
                comando.Parameters.AddWithValue("@fecha_entrada", fechaEntrada.Date);
                comando.Parameters.AddWithValue("@fecha_salida", fechaSalida.Date);
                comando.Parameters.AddWithValue("@cantidad_personas", modelo.CantidadPersonas);
                comando.Parameters.AddWithValue("@total_reserva", totalReserva);
                comando.Parameters.AddWithValue("@saldo_pendiente", totalReserva);
                comando.Parameters.AddWithValue("@observaciones",
                    string.IsNullOrWhiteSpace(modelo.Observaciones) ? DBNull.Value : modelo.Observaciones.Trim());
                comando.ExecuteNonQuery();
                idReserva = Convert.ToInt32(comando.LastInsertedId);
            }

            string codigoSeguridad = GenerarCodigoSeguridad(idReserva, modelo.IdHabitacion);

            using (var comandoCodigo = new MySqlCommand(@"
                UPDATE reserva
                SET codigo_seguridad = @codigo
                WHERE id_reserva = @id_reserva;", conexion, transaccion))
            {
                comandoCodigo.Parameters.AddWithValue("@codigo", codigoSeguridad);
                comandoCodigo.Parameters.AddWithValue("@id_reserva", idReserva);
                comandoCodigo.ExecuteNonQuery();
            }

            if (registrarCuentaIndividual && totalReserva > 0)
            {
                RegistrarMovimientoReserva(
                    conexion,
                    transaccion,
                    idReserva,
                    idUsuario,
                    modelo.IdClipro,
                    modelo.IdHabitacion,
                    idFormaPagoCredito,
                    idTipoMovimientoCxc,
                    totalReserva,
                    $"Cuenta por cobrar generada para reserva #{idReserva}",
                    modelo.Observaciones);
            }

            return idReserva;
        }

        private void RegistrarCuentaReservaGrupo(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idReservaGrupo,
            ReservaFormViewModel modelo,
            IReadOnlyCollection<DateTime> fechas,
            decimal precioPorNoche,
            int idUsuario,
            int idTipoMovimientoCxc,
            int idFormaPagoCredito)
        {
            decimal precioPorFecha = precioPorNoche * modelo.CantidadPersonas;
            decimal totalGrupo = precioPorFecha * fechas.Count;

            const string insertarMovimiento = @"
                INSERT INTO movimiento
                (
                    id_usuario,
                    id_clipro,
                    id_tipomov,
                    id_formapago,
                    id_reserva,
                    id_reserva_grupo,
                    estado,
                    observaciones
                )
                VALUES
                (
                    @id_usuario,
                    @id_clipro,
                    @id_tipomov,
                    @id_formapago,
                    NULL,
                    @id_reserva_grupo,
                    'activo',
                    @observaciones
                );";

            int idMovimiento;

            using (var comando = new MySqlCommand(insertarMovimiento, conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_usuario", idUsuario);
                comando.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                comando.Parameters.AddWithValue("@id_tipomov", idTipoMovimientoCxc);
                comando.Parameters.AddWithValue("@id_formapago", idFormaPagoCredito);
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                comando.Parameters.AddWithValue("@observaciones",
                    string.IsNullOrWhiteSpace(modelo.Observaciones) ? DBNull.Value : modelo.Observaciones.Trim());
                comando.ExecuteNonQuery();
                idMovimiento = Convert.ToInt32(comando.LastInsertedId);
            }

            string fechasDescripcion = string.Join(", ", fechas.OrderBy(fecha => fecha).Select(fecha => fecha.ToString("dd/MM/yyyy")));
            string descripcion = $"Hospedajes del grupo #{idReservaGrupo}: {fechasDescripcion}";

            if (descripcion.Length > 255)
            {
                descripcion = descripcion[..252] + "...";
            }

            using var comandoDetalle = new MySqlCommand(@"
                INSERT INTO detalle
                (
                    id_movimiento,
                    id_proser,
                    cantidad,
                    precio_unitario,
                    subtotal,
                    descripcion
                )
                VALUES
                (
                    @id_movimiento,
                    @id_proser,
                    @cantidad,
                    @precio_unitario,
                    @subtotal,
                    @descripcion
                );", conexion, transaccion);

            comandoDetalle.Parameters.AddWithValue("@id_movimiento", idMovimiento);
            comandoDetalle.Parameters.AddWithValue("@id_proser", modelo.IdHabitacion);
            comandoDetalle.Parameters.AddWithValue("@cantidad", fechas.Count);
            comandoDetalle.Parameters.AddWithValue("@precio_unitario", precioPorFecha);
            comandoDetalle.Parameters.AddWithValue("@subtotal", totalGrupo);
            comandoDetalle.Parameters.AddWithValue("@descripcion", descripcion);
            comandoDetalle.ExecuteNonQuery();
        }

       

        // ============================================================
        // INDEX ADMINISTRATIVO
        // ============================================================

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "fecha_entrada",
            string direccion = "desc",
            string vista = "pendiente",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaReservas = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using (var comandoVencidas = new MySqlCommand(@"
                    SELECT COUNT(*)
                    FROM reserva
                    WHERE estado = 'pendiente'
                      AND fecha_entrada < CURDATE();", conexion))
                {
                    ViewBag.ReservasPendientesVencidas = Convert.ToInt32(comandoVencidas.ExecuteScalar());
                }

                string vistaNormalizada = vista.Trim().ToLower();
                string[] estadosPermitidos = { "todas", "pendiente", "en_curso", "en_checkout", "finalizada", "pendientes_factura", "cancelada" };

                if (!estadosPermitidos.Contains(vistaNormalizada))
                {
                    vistaNormalizada = "pendiente";
                }

                string columnaOrden = ObtenerColumnaOrden(ordenarPor);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string condicionEstado = vistaNormalizada switch
                {
                    "todas" => "",
                    "pendientes_factura" => "AND rf.estado_facturacion IN ('pendiente', 'anulada')",
                    _ => "AND r.estado = @estado"
                };
                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    condicionBusqueda = @"
                        AND (
                            c.nombre LIKE @busqueda
                            OR ec.nombre LIKE @busqueda
                            OR p.codigo LIKE @busqueda
                            OR CAST(r.id_reserva_grupo AS CHAR) LIKE @busqueda
                            OR r.observaciones LIKE @busqueda
                        ) ";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    LEFT JOIN cliente_detalle cd ON c.id_clipro = cd.id_clipro
                    LEFT JOIN empresa_cliente ec ON cd.id_empresa_cliente = ec.id_empresa_cliente
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    LEFT JOIN reserva_facturacion rf ON rf.id_reserva = r.id_reserva
                    WHERE 1 = 1
                    {condicionEstado}
                    {condicionBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);

                if (vistaNormalizada is not ("todas" or "pendientes_factura"))
                {
                    comandoConteo.Parameters.AddWithValue("@estado", vistaNormalizada);
                }

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comandoConteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                int totalRegistros = Convert.ToInt32(comandoConteo.ExecuteScalar());
                int totalPaginas = (int)Math.Ceiling((double)totalRegistros / RegistrosPorPagina);

                if (totalPaginas == 0)
                {
                    totalPaginas = 1;
                }

                if (pagina > totalPaginas)
                {
                    pagina = totalPaginas;
                }

                int offset = (pagina - 1) * RegistrosPorPagina;

                string consulta = $@"
                    SELECT
                        r.id_reserva,
                        r.id_reserva_grupo,
                        c.nombre,
                        ec.nombre AS empresa_procedencia,
                        p.codigo AS habitacion,
                        r.fecha_entrada,
                        r.fecha_salida,
                        r.fecha_hora_checkin,
                        r.fecha_hora_checkout,
                        r.total_reserva,
                        r.saldo_pendiente,
                        COALESCE(cd.solicita_limpieza, 1) AS solicita_limpieza,
                        r.estado,
                        r.observaciones,
                        COALESCE(rf.estado_facturacion, 'sin_definir') AS estado_facturacion,
                        EXISTS(
                            SELECT 1
                            FROM documento_fiscal_reserva dfr
                            INNER JOIN documento_fiscal df
                                ON df.id_documento_fiscal = dfr.id_documento_fiscal
                            WHERE dfr.id_reserva = r.id_reserva
                              AND df.tipo_documento = 'factura'
                              AND df.estado = 'vigente'
                        ) AS tiene_factura
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    LEFT JOIN cliente_detalle cd ON c.id_clipro = cd.id_clipro
                    LEFT JOIN empresa_cliente ec ON cd.id_empresa_cliente = ec.id_empresa_cliente
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    LEFT JOIN reserva_facturacion rf ON rf.id_reserva = r.id_reserva
                    WHERE 1 = 1
                    {condicionEstado}
                    {condicionBusqueda}
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);

                if (vistaNormalizada is not ("todas" or "pendientes_factura"))
                {
                    comando.Parameters.AddWithValue("@estado", vistaNormalizada);
                }

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tablaReservas);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = direccionOrden.ToLower();
                ViewBag.Vista = vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar las reservas: " + ex.Message;
            }

            return View(tablaReservas);
        }

        // ============================================================
        // MIS RESERVAS - CLIENTE
        // ============================================================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult MisReservas(int pagina = 1)
        {
            IActionResult? acceso = ValidarAccesoSoloCliente();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaReservas = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idClipro = ObtenerIdClienteSesion();

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string consultaConteo = @"
                    SELECT COUNT(*)
                    FROM reserva r
                    WHERE r.id_clipro = @id_clipro;";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);
                comandoConteo.Parameters.AddWithValue("@id_clipro", idClipro);

                int totalRegistros = Convert.ToInt32(comandoConteo.ExecuteScalar());
                int totalPaginas = (int)Math.Ceiling((double)totalRegistros / RegistrosPorPagina);

                if (totalPaginas == 0)
                {
                    totalPaginas = 1;
                }

                if (pagina > totalPaginas)
                {
                    pagina = totalPaginas;
                }

                int offset = (pagina - 1) * RegistrosPorPagina;

                string consulta = @"
                    SELECT
                        r.id_reserva,
                        p.codigo AS habitacion,
                        r.fecha_entrada,
                        r.fecha_salida,
                        r.fecha_hora_checkin,
                        r.fecha_hora_checkout,
                        r.cantidad_personas,
                        r.total_reserva,
                        r.saldo_pendiente,
                        r.estado
                    FROM reserva r
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    WHERE r.id_clipro = @id_clipro
                    ORDER BY r.id_reserva DESC
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id_clipro", idClipro);
                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tablaReservas);

                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar sus reservaciones: " + ex.Message;
            }

            return View(tablaReservas);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult MiReservaDetalle(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloCliente();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                ReservaDetalleViewModel? modelo = ObtenerReservaDetallePorId(conexion, id, ObtenerIdClienteSesion());

                if (modelo == null)
                {
                    TempData["Mensaje"] = "No se encontró la reservación solicitada.";
                    return RedirectToAction("MisReservas");
                }

                if (modelo.IdReservaGrupo.HasValue)
                {
                    modelo.ReservasDelGrupo = ObtenerReservasDelGrupo(conexion, modelo.IdReservaGrupo.Value);
                }

                try
                {
                    using var conexionPagos = _conexionBD.ObtenerConexion();
                    conexionPagos.Open();
                    modelo.TotalPagado = ObtenerTotalPagadoReserva(conexionPagos, id);
                }
                catch
                {
                    modelo.TotalPagado = 0;
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el detalle de la reservación: " + ex.Message;
                return RedirectToAction("MisReservas");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult CancelarMiReserva(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloCliente();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idCliproSesion = ObtenerIdClienteSesion();

                string consultaEstado = @"
                    SELECT estado
                    FROM reserva
                    WHERE id_reserva = @id
                      AND id_clipro = @id_clipro
                    LIMIT 1;";

                using var comandoEstado = new MySqlCommand(consultaEstado, conexion);
                comandoEstado.Parameters.AddWithValue("@id", id);
                comandoEstado.Parameters.AddWithValue("@id_clipro", idCliproSesion);

                object? resultadoEstado = comandoEstado.ExecuteScalar();

                if (resultadoEstado == null)
                {
                    TempData["Mensaje"] = "No se encontró la reservación solicitada.";
                    return RedirectToAction("MisReservas");
                }

                string estadoActual = resultadoEstado.ToString()?.Trim().ToLower() ?? "";

                if (estadoActual != "pendiente")
                {
                    TempData["Mensaje"] = "Solo se pueden cancelar reservaciones pendientes de ingreso.";
                    return RedirectToAction("MisReservas");
                }

                string consulta = @"
                    UPDATE reserva
                    SET estado = 'cancelada'
                    WHERE id_reserva = @id
                      AND id_clipro = @id_clipro
                      AND estado = 'pendiente';";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@id_clipro", idCliproSesion);

                int filas = comando.ExecuteNonQuery();

                if (filas == 0)
                {
                    TempData["Mensaje"] = "No se pudo cancelar la reservación.";
                }
                else
                {
                    TempData["Exito"] = "Reservación cancelada correctamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cancelar la reservación: " + ex.Message;
            }

            return RedirectToAction("MisReservas");
        }

        // ============================================================
        // DETALLE ADMINISTRATIVO
        // ============================================================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                ReservaDetalleViewModel? modelo = ObtenerReservaDetallePorId(conexion, id);

                if (modelo == null)
                {
                    TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                    return RedirectToAction("Index");
                }

                if (modelo.IdReservaGrupo.HasValue)
                {
                    modelo.ReservasDelGrupo = ObtenerReservasDelGrupo(conexion, modelo.IdReservaGrupo.Value);
                }

                CargarDatosFacturacion(conexion, modelo);

                try
                {
                    using var conexionPagos = _conexionBD.ObtenerConexion();
                    conexionPagos.Open();
                    modelo.TotalPagado = ObtenerTotalPagadoReserva(conexionPagos, id);
                }
                catch
                {
                    modelo.TotalPagado = 0;
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cargar el detalle de la reserva: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult SubirFactura(int id, IFormFile? facturaPdf)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            if (facturaPdf == null || facturaPdf.Length == 0)
            {
                TempData["Mensaje"] = "Seleccione una factura en formato PDF.";
                return RedirectToAction("Detalle", new { id });
            }

            const long tamanoMaximo = 10L * 1024L * 1024L;

            if (facturaPdf.Length > tamanoMaximo)
            {
                TempData["Mensaje"] = "La factura no puede superar los 10 MB.";
                return RedirectToAction("Detalle", new { id });
            }

            string nombreOriginal = Path.GetFileName(facturaPdf.FileName).Trim();

            if (!string.Equals(Path.GetExtension(nombreOriginal), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "El archivo seleccionado debe tener formato PDF.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.IsNullOrWhiteSpace(nombreOriginal))
            {
                nombreOriginal = $"Factura-reserva-{id}.pdf";
            }

            if (nombreOriginal.Length > 255)
            {
                nombreOriginal = nombreOriginal[..251] + ".pdf";
            }

            byte[] contenido;

            using (var memoria = new MemoryStream())
            {
                facturaPdf.CopyTo(memoria);
                contenido = memoria.ToArray();
            }

            if (contenido.Length < 5 ||
                contenido[0] != (byte)'%' ||
                contenido[1] != (byte)'P' ||
                contenido[2] != (byte)'D' ||
                contenido[3] != (byte)'F' ||
                contenido[4] != (byte)'-')
            {
                TempData["Mensaje"] = "El archivo seleccionado no contiene un PDF válido.";
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                DestinoFacturaReserva? destino = ObtenerDestinoFactura(
                    conexion,
                    id,
                    transaccion,
                    bloquear: true);

                if (destino == null)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "No se encontró la reservación.";
                    return RedirectToAction("Index");
                }

                const string consulta = @"
                    INSERT INTO reserva_factura
                        (id_reserva, id_reserva_grupo, contenido, tipo_mime,
                         nombre_original, tamano, fecha_subida, id_usuario)
                    VALUES
                        (@id_reserva, @id_reserva_grupo, @contenido, 'application/pdf',
                         @nombre_original, @tamano, CURRENT_TIMESTAMP, @id_usuario)
                    ON DUPLICATE KEY UPDATE
                        contenido = VALUES(contenido),
                        tipo_mime = VALUES(tipo_mime),
                        nombre_original = VALUES(nombre_original),
                        tamano = VALUES(tamano),
                        fecha_subida = CURRENT_TIMESTAMP,
                        id_usuario = VALUES(id_usuario);";

                using (var comando = new MySqlCommand(consulta, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue(
                        "@id_reserva",
                        destino.IdReservaGrupo.HasValue ? DBNull.Value : destino.IdReserva);
                    comando.Parameters.AddWithValue(
                        "@id_reserva_grupo",
                        destino.IdReservaGrupo.HasValue ? destino.IdReservaGrupo.Value : DBNull.Value);
                    comando.Parameters.Add("@contenido", MySqlDbType.LongBlob).Value = contenido;
                    comando.Parameters.AddWithValue("@nombre_original", nombreOriginal);
                    comando.Parameters.AddWithValue("@tamano", contenido.LongLength);
                    comando.Parameters.AddWithValue("@id_usuario", ObtenerIdUsuarioSesion());
                    comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                TempData["Exito"] = destino.IdReservaGrupo.HasValue
                    ? "Factura guardada para todas las estadías de la reserva agrupada."
                    : "Factura guardada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible guardar la factura: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { id });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult FacturaPdf(int id, bool descargar = false)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                DestinoFacturaReserva? destino = ObtenerDestinoFactura(conexion, id);

                if (destino == null)
                {
                    TempData["Mensaje"] = "No se encontró la reservación.";
                    return RedirectToAction("Index");
                }

                const string consulta = @"
                    SELECT contenido, nombre_original
                    FROM reserva_factura
                    WHERE
                        (@id_reserva_grupo IS NOT NULL AND id_reserva_grupo = @id_reserva_grupo)
                        OR
                        (@id_reserva_grupo IS NULL AND id_reserva = @id_reserva)
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id_reserva", destino.IdReserva);
                comando.Parameters.AddWithValue(
                    "@id_reserva_grupo",
                    destino.IdReservaGrupo.HasValue ? destino.IdReservaGrupo.Value : DBNull.Value);
                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "Esta reservación todavía no tiene una factura adjunta.";
                    return RedirectToAction("Detalle", new { id });
                }

                byte[] contenido = (byte[])lector["contenido"];
                string nombre = lector["nombre_original"]?.ToString() ?? $"Factura-reserva-{id}.pdf";

                return descargar
                    ? File(contenido, "application/pdf", nombre)
                    : File(contenido, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible abrir la factura: " + ex.Message;
                return RedirectToAction("Detalle", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult EliminarFactura(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                DestinoFacturaReserva? destino = ObtenerDestinoFactura(
                    conexion,
                    id,
                    transaccion,
                    bloquear: true);

                if (destino == null)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "No se encontró la reservación.";
                    return RedirectToAction("Index");
                }

                const string consulta = @"
                    DELETE FROM reserva_factura
                    WHERE
                        (@id_reserva_grupo IS NOT NULL AND id_reserva_grupo = @id_reserva_grupo)
                        OR
                        (@id_reserva_grupo IS NULL AND id_reserva = @id_reserva);";

                using var comando = new MySqlCommand(consulta, conexion, transaccion);
                comando.Parameters.AddWithValue("@id_reserva", destino.IdReserva);
                comando.Parameters.AddWithValue(
                    "@id_reserva_grupo",
                    destino.IdReservaGrupo.HasValue ? destino.IdReservaGrupo.Value : DBNull.Value);

                int eliminados = comando.ExecuteNonQuery();
                transaccion.Commit();

                TempData[eliminados > 0 ? "Exito" : "Mensaje"] = eliminados > 0
                    ? "Factura eliminada correctamente."
                    : "Esta reservación no tenía una factura adjunta.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible eliminar la factura: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { id });
        }

        // ============================================================
        // CONFIRMACION CLIENTE
        // ============================================================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [NonAction]
        public IActionResult ConfirmacionCliente(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            if (!EsClienteSesion())
            {
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                ReservaDetalleViewModel? modelo = ObtenerReservaDetallePorId(conexion, id, ObtenerIdClienteSesion());

                if (modelo == null)
                {
                    TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                    return RedirectToAction("Index", "Panel");
                }

                if (modelo.IdReservaGrupo.HasValue)
                {
                    modelo.ReservasDelGrupo = ObtenerReservasDelGrupo(conexion, modelo.IdReservaGrupo.Value);
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar la confirmación de la reserva: " + ex.Message;
                return RedirectToAction("Index", "Panel");
            }
        }

        // ============================================================
        // CREAR RESERVA
        // ============================================================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(
            int? idHabitacion = null,
            int? idClipro = null,
            DateTime? fechaEntrada = null,
            DateTime? fechaSalida = null,
            int? cantidadPersonas = null,
            string? observaciones = null,
            bool usarFechasSeparadas = false,
            List<DateTime>? fechasSeparadas = null)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            bool esCliente = EsClienteSesion();

            if (esCliente && !idHabitacion.HasValue)
            {
                TempData["Mensaje"] = "Para crear una reserva debe seleccionar primero una habitación disponible.";
                return RedirectToAction("Index", "Disponibilidad");
            }

            ReservaFormViewModel modelo = new ReservaFormViewModel
            {
                FechaEntrada = fechaEntrada ?? DateTime.Today,
                FechaSalida = fechaSalida ?? DateTime.Today.AddDays(1),
                UsarFechasSeparadas = !esCliente && usarFechasSeparadas,
                FechasSeparadas = !esCliente
                    ? NormalizarFechasSeparadas(fechasSeparadas)
                    : new List<DateTime>()
            };

            if (idHabitacion.HasValue) modelo.IdHabitacion = idHabitacion.Value;
            if (cantidadPersonas.HasValue) modelo.CantidadPersonas = cantidadPersonas.Value;
            if (!string.IsNullOrWhiteSpace(observaciones)) modelo.Observaciones = observaciones;

            if (esCliente)
            {
                modelo.IdClipro = ObtenerIdClienteSesion();

                if (!ClienteTieneFlujoCrearValido(modelo, out string mensajeFlujo))
                {
                    TempData["Mensaje"] = mensajeFlujo;
                    return RedirectToAction("Index", "Disponibilidad");
                }

                if (!HabitacionSigueDisponibleParaCrear(modelo.IdHabitacion, modelo.FechaEntrada, modelo.FechaSalida))
                {
                    TempData["Mensaje"] = "La habitación seleccionada ya no está disponible para esas fechas.";
                    return RedirectToAction("Index", "Disponibilidad");
                }

                string tokenFlujoCliente = GenerarTokenFlujoCliente(
                    modelo.IdHabitacion,
                    modelo.FechaEntrada,
                    modelo.FechaSalida
                );

                CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
                ConfigurarVistaCrear(modelo, true, tokenFlujoCliente);

                return View(modelo);
            }

            if (idClipro.HasValue)
            {
                modelo.IdClipro = idClipro.Value;
            }

            if (modelo.UsarFechasSeparadas)
            {
                CargarCombos(null, null, modelo.IdHabitacion);
            }
            else
            {
                CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
            }
            ConfigurarVistaCrear(modelo, false);

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(ReservaFormViewModel modelo, string? tokenFlujoCliente = null)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            bool esCliente = EsClienteSesion();

            if (esCliente)
            {
                modelo.IdClipro = ObtenerIdClienteSesion();
            }

            if (modelo.UsarFechasSeparadas)
            {
                CargarCombos(null, null, modelo.IdHabitacion);
            }
            else
            {
                CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
            }
            ConfigurarVistaCrear(modelo, esCliente, tokenFlujoCliente);

            if (esCliente && modelo.UsarFechasSeparadas)
            {
                TempData["Mensaje"] = "Las reservas con fechas separadas deben ser registradas por recepción.";
                return RedirectToAction("Index", "Disponibilidad");
            }

            if (esCliente)
            {
                if (!ClienteTieneFlujoCrearValido(modelo, out string mensajeFlujo))
                {
                    TempData["Mensaje"] = mensajeFlujo;
                    return RedirectToAction("Index", "Disponibilidad");
                }

                if (!TokenFlujoClienteEsValido(
                    tokenFlujoCliente,
                    modelo.IdHabitacion,
                    modelo.FechaEntrada,
                    modelo.FechaSalida))
                {
                    TempData["Mensaje"] = "El flujo de la reserva no es válido o fue alterado. Seleccione nuevamente la habitación desde disponibilidad.";
                    return RedirectToAction("Index", "Disponibilidad");
                }

                if (!HabitacionSigueDisponibleParaCrear(
                    modelo.IdHabitacion,
                    modelo.FechaEntrada,
                    modelo.FechaSalida))
                {
                    TempData["Mensaje"] = "La habitación seleccionada ya no está disponible para esas fechas.";
                    return RedirectToAction("Index", "Disponibilidad");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                List<DateTime> fechasSeparadas = new();

                if (modelo.UsarFechasSeparadas)
                {
                    if (!FechasSeparadasSonValidas(modelo.FechasSeparadas, out fechasSeparadas, out string mensajeFechasSeparadas))
                    {
                        ModelState.AddModelError("", mensajeFechasSeparadas);
                        return View(modelo);
                    }

                    modelo.FechasSeparadas = fechasSeparadas;
                }
                else if (!FechasReservaSonValidas(modelo.FechaEntrada, modelo.FechaSalida, out string mensajeFechas))
                {
                    ModelState.AddModelError("", mensajeFechas);
                    return View(modelo);
                }

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string validarCliente = @"
                    SELECT COUNT(*)
                    FROM clipro
                    WHERE id_clipro = @id_clipro
                      AND id_tipoclipro = @id_tipoclipro
                      AND estado = 'activo';";

                using (var comandoCliente = new MySqlCommand(validarCliente, conexion))
                {
                    comandoCliente.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                    comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                    int existeCliente = Convert.ToInt32(comandoCliente.ExecuteScalar());

                    if (existeCliente == 0)
                    {
                        ModelState.AddModelError("", "El cliente seleccionado no es válido o está inactivo.");
                        return View(modelo);
                    }
                }

                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string validarHabitacion = @"
                    SELECT COUNT(*)
                    FROM proser
                    WHERE id_proser = @id_habitacion
                      AND id_tipoproser = @id_tipoproser;";

                using (var comandoHabitacion = new MySqlCommand(validarHabitacion, conexion))
                {
                    comandoHabitacion.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                    comandoHabitacion.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                    int existeHabitacion = Convert.ToInt32(comandoHabitacion.ExecuteScalar());

                    if (existeHabitacion == 0)
                    {
                        ModelState.AddModelError("", "La habitación seleccionada no es válida.");
                        return View(modelo);
                    }
                }

                if (modelo.UsarFechasSeparadas)
                {
                    foreach (DateTime fecha in fechasSeparadas)
                    {
                        if (!HabitacionSigueDisponibleParaCrear(modelo.IdHabitacion, fecha, fecha.AddDays(1)))
                        {
                            ModelState.AddModelError("", $"La habitación ya no está disponible para el {fecha:dd/MM/yyyy}.");
                            return View(modelo);
                        }
                    }
                }
                else if (!HabitacionSigueDisponibleParaCrear(modelo.IdHabitacion, modelo.FechaEntrada, modelo.FechaSalida))
                {
                    ModelState.AddModelError("", "La habitación ya no está disponible en esas fechas.");
                    return View(modelo);
                }

                decimal precioPorNoche = ObtenerPrecioHabitacion(conexion, modelo.IdHabitacion);
                int idUsuario = ObtenerIdUsuarioValidoParaMovimiento(conexion);
                int idTipoMovimientoCxc = ObtenerIdTipoMovimiento(conexion, "cuenta_por_cobrar");
                int idFormaPagoCredito = ObtenerIdFormaPago(conexion, "credito");
                int idReservaGenerada;

                using var transaccion = conexion.BeginTransaction();

                using (var comandoBloqueo = new MySqlCommand(@"
                    SELECT id_proser
                    FROM proser
                    WHERE id_proser = @id_habitacion
                    FOR UPDATE;", conexion, transaccion))
                {
                    comandoBloqueo.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);

                    if (comandoBloqueo.ExecuteScalar() == null)
                    {
                        transaccion.Rollback();
                        ModelState.AddModelError("", "La habitación seleccionada ya no existe.");
                        return View(modelo);
                    }
                }

                if (modelo.UsarFechasSeparadas)
                {
                    foreach (DateTime fecha in fechasSeparadas)
                    {
                        if (!HabitacionSigueDisponibleParaCrear(modelo.IdHabitacion, fecha, fecha.AddDays(1)))
                        {
                            transaccion.Rollback();
                            ModelState.AddModelError("", $"La habitación dejó de estar disponible para el {fecha:dd/MM/yyyy}. No se guardó ninguna fecha del grupo.");
                            return View(modelo);
                        }
                    }
                }
                else if (!HabitacionSigueDisponibleParaCrear(modelo.IdHabitacion, modelo.FechaEntrada, modelo.FechaSalida))
                {
                    transaccion.Rollback();
                    ModelState.AddModelError("", "La habitación dejó de estar disponible para esas fechas.");
                    return View(modelo);
                }

                if (modelo.UsarFechasSeparadas)
                {
                    int idReservaGrupo;

                    using (var comandoGrupo = new MySqlCommand(@"
                        INSERT INTO reserva_grupo (id_clipro, observaciones)
                        VALUES (@id_clipro, @observaciones);", conexion, transaccion))
                    {
                        comandoGrupo.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                        comandoGrupo.Parameters.AddWithValue("@observaciones",
                            string.IsNullOrWhiteSpace(modelo.Observaciones) ? DBNull.Value : modelo.Observaciones.Trim());
                        comandoGrupo.ExecuteNonQuery();
                        idReservaGrupo = Convert.ToInt32(comandoGrupo.LastInsertedId);
                    }

                    idReservaGenerada = 0;

                    foreach (DateTime fecha in fechasSeparadas)
                    {
                        int idReservaCreada = InsertarReservaConCuenta(
                            conexion,
                            transaccion,
                            modelo,
                            fecha,
                            fecha.AddDays(1),
                            precioPorNoche,
                            idReservaGrupo,
                            idUsuario,
                            idTipoMovimientoCxc,
                            idFormaPagoCredito,
                            registrarCuentaIndividual: false);

                        if (idReservaGenerada == 0)
                        {
                            idReservaGenerada = idReservaCreada;
                        }
                    }

                    RegistrarCuentaReservaGrupo(
                        conexion,
                        transaccion,
                        idReservaGrupo,
                        modelo,
                        fechasSeparadas,
                        precioPorNoche,
                        idUsuario,
                        idTipoMovimientoCxc,
                        idFormaPagoCredito);
                }
                else
                {
                    idReservaGenerada = InsertarReservaConCuenta(
                        conexion,
                        transaccion,
                        modelo,
                        modelo.FechaEntrada,
                        modelo.FechaSalida,
                        precioPorNoche,
                        null,
                        idUsuario,
                        idTipoMovimientoCxc,
                        idFormaPagoCredito);
                }

                transaccion.Commit();

                if (esCliente)
                {
                    TempData["Exito"] = "Su reserva fue creada correctamente.";
                    return RedirectToAction("ConfirmacionCliente", new { id = idReservaGenerada });
                }

                TempData["Exito"] = modelo.UsarFechasSeparadas
                    ? $"Reserva agrupada creada correctamente con {fechasSeparadas.Count} estadías."
                    : "Reserva creada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar la reserva: " + ex.Message;
                return View(modelo);
            }
        }

        //Generar código de seguridad
        private string GenerarCodigoSeguridad(int idReserva, int idHabitacion)
        {
            string hora = DateTime.Now.ToString("HHmmss");
            string random = Random.Shared.Next(100, 999).ToString();

            return $"RES-{idReserva}-H{idHabitacion}-{hora}-{random}";
        }

        // ============================================================
        // EDITAR RESERVA - ADMIN
        // ============================================================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        id_reserva,
                        id_clipro,
                        id_habitacion,
                        precio_noche_aplicado,
                        fecha_entrada,
                        fecha_salida,
                        cantidad_personas,
                        observaciones,
                        estado,
                        total_reserva
                    FROM reserva
                    WHERE id_reserva = @id
                    LIMIT 1
                    FOR UPDATE;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                    return RedirectToAction("Index");
                }

                string estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";

                if (estado != "pendiente")
                {
                    TempData["Mensaje"] = "Solo se pueden editar reservas pendientes de ingreso.";
                    return RedirectToAction("Index");
                }

                ReservaFormViewModel modelo = new ReservaFormViewModel
                {
                    IdReserva = Convert.ToInt32(lector["id_reserva"]),
                    IdClipro = Convert.ToInt32(lector["id_clipro"]),
                    IdHabitacion = Convert.ToInt32(lector["id_habitacion"]),
                    IdHabitacionAnterior = Convert.ToInt32(lector["id_habitacion"]),
                    PrecioNocheAplicado = Convert.ToDecimal(lector["precio_noche_aplicado"]),
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    CantidadPersonas = Convert.ToInt32(lector["cantidad_personas"]),
                    Observaciones = lector["observaciones"] == DBNull.Value
                        ? null
                        : lector["observaciones"]?.ToString(),
                    TotalReserva = Convert.ToDecimal(lector["total_reserva"])
                };

                CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar la reserva: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(ReservaFormViewModel modelo)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consultaReservaActual = @"
                    SELECT
                        id_reserva,
                        id_reserva_grupo,
                        id_clipro,
                        id_habitacion,
                        precio_noche_aplicado,
                        estado
                    FROM reserva
                    WHERE id_reserva = @id_reserva
                    LIMIT 1;";

                int idHabitacionAnteriorBd = 0;
                int idClienteAnteriorBd = 0;
                int? idReservaGrupoBd = null;
                decimal precioHistoricoBd = 0;

                using (var comandoEstado = new MySqlCommand(consultaReservaActual, conexion))
                {
                    comandoEstado.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);

                    using var lector = comandoEstado.ExecuteReader();

                    if (!lector.Read())
                    {
                        TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                        return RedirectToAction("Index");
                    }

                    string estadoActual = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";

                    if (estadoActual != "pendiente")
                    {
                        TempData["Mensaje"] = "Solo se pueden editar reservas pendientes de ingreso.";
                        return RedirectToAction("Index");
                    }

                    idHabitacionAnteriorBd = Convert.ToInt32(lector["id_habitacion"]);
                    idClienteAnteriorBd = Convert.ToInt32(lector["id_clipro"]);
                    idReservaGrupoBd = lector["id_reserva_grupo"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(lector["id_reserva_grupo"]);
                    precioHistoricoBd = Convert.ToDecimal(lector["precio_noche_aplicado"]);
                }

                modelo.IdHabitacionAnterior = idHabitacionAnteriorBd;
                modelo.PrecioNocheAplicado = precioHistoricoBd;

                if (!FechasReservaSonValidas(modelo.FechaEntrada, modelo.FechaSalida, out string mensajeFechas))
                {
                    ModelState.AddModelError("", mensajeFechas);
                    return View(modelo);
                }

                int noches = (modelo.FechaSalida.Date - modelo.FechaEntrada.Date).Days;

                if (noches <= 0)
                {
                    ModelState.AddModelError("", "La reserva debe tener al menos una noche.");
                    return View(modelo);
                }

                if (idReservaGrupoBd.HasValue)
                {
                    if (modelo.IdClipro != idClienteAnteriorBd || modelo.IdHabitacion != idHabitacionAnteriorBd)
                    {
                        ModelState.AddModelError("", "Las estadías de un grupo deben conservar el mismo cliente y la misma habitación.");
                        return View(modelo);
                    }

                    if (noches != 1)
                    {
                        ModelState.AddModelError("", "Cada fecha de una reserva agrupada representa una sola noche.");
                        return View(modelo);
                    }

                    var fechasGrupo = new List<DateTime> { modelo.FechaEntrada.Date };
                    using (var comandoFechasGrupo = new MySqlCommand(@"
                        SELECT fecha_entrada
                        FROM reserva
                        WHERE id_reserva_grupo = @id_reserva_grupo
                          AND id_reserva <> @id_reserva
                        ORDER BY fecha_entrada;", conexion))
                    {
                        comandoFechasGrupo.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupoBd.Value);
                        comandoFechasGrupo.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);
                        using var lectorFechasGrupo = comandoFechasGrupo.ExecuteReader();

                        while (lectorFechasGrupo.Read())
                        {
                            fechasGrupo.Add(Convert.ToDateTime(lectorFechasGrupo["fecha_entrada"]).Date);
                        }
                    }

                    if (!FechasSeparadasSonValidas(fechasGrupo, out _, out string mensajeGrupo))
                    {
                        ModelState.AddModelError("", mensajeGrupo);
                        return View(modelo);
                    }
                }

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string validarCliente = @"
                    SELECT COUNT(*)
                    FROM clipro
                    WHERE id_clipro = @id_clipro
                      AND id_tipoclipro = @id_tipoclipro
                      AND estado = 'activo';";

                using (var comandoCliente = new MySqlCommand(validarCliente, conexion))
                {
                    comandoCliente.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                    comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                    int existeCliente = Convert.ToInt32(comandoCliente.ExecuteScalar());

                    if (existeCliente == 0)
                    {
                        ModelState.AddModelError("", "El cliente seleccionado no es válido o está inactivo.");
                        return View(modelo);
                    }
                }

                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string validarHabitacion = @"
                    SELECT COUNT(*)
                    FROM proser
                    WHERE id_proser = @id_habitacion
                      AND id_tipoproser = @id_tipoproser;";

                using (var comandoHabitacion = new MySqlCommand(validarHabitacion, conexion))
                {
                    comandoHabitacion.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                    comandoHabitacion.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                    int existeHabitacion = Convert.ToInt32(comandoHabitacion.ExecuteScalar());

                    if (existeHabitacion == 0)
                    {
                        ModelState.AddModelError("", "La habitación seleccionada no es válida.");
                        return View(modelo);
                    }
                }

                bool habitacionDisponible = _disponibilidadService.EstaHabitacionDisponible(
                    modelo.IdHabitacion,
                    modelo.FechaEntrada,
                    modelo.FechaSalida,
                    modelo.IdReserva
                );

                if (!habitacionDisponible)
                {
                    ModelState.AddModelError("", "La habitación ya no está disponible en esas fechas.");
                    return View(modelo);
                }

                bool cambioHabitacion = modelo.IdHabitacion != modelo.IdHabitacionAnterior;

                decimal precioPorNoche;

                if (cambioHabitacion)
                {
                    precioPorNoche = ObtenerPrecioHabitacion(conexion, modelo.IdHabitacion);
                }
                else
                {
                    precioPorNoche = modelo.PrecioNocheAplicado;
                }

                decimal totalReserva = precioPorNoche * noches * modelo.CantidadPersonas;
                decimal totalPagado = ObtenerTotalPagadoReserva(conexion, modelo.IdReserva);
                decimal saldoPendiente = totalReserva - totalPagado;

                if (saldoPendiente < 0)
                {
                    ModelState.AddModelError("", "Los pagos ya registrados superan el nuevo total de la reserva.");
                    return View(modelo);
                }

                string actualizar = @"
                    UPDATE reserva
                    SET id_clipro = @id_clipro,
                        id_habitacion = @id_habitacion,
                        precio_noche_aplicado = @precio_noche_aplicado,
                        fecha_entrada = @fecha_entrada,
                        fecha_salida = @fecha_salida,
                        cantidad_personas = @cantidad_personas,
                        total_reserva = @total_reserva,
                        saldo_pendiente = @saldo_pendiente,
                        observaciones = @observaciones
                    WHERE id_reserva = @id_reserva;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                comandoActualizar.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                comandoActualizar.Parameters.AddWithValue("@precio_noche_aplicado", precioPorNoche);
                comandoActualizar.Parameters.AddWithValue("@fecha_entrada", modelo.FechaEntrada.Date);
                comandoActualizar.Parameters.AddWithValue("@fecha_salida", modelo.FechaSalida.Date);
                comandoActualizar.Parameters.AddWithValue("@cantidad_personas", modelo.CantidadPersonas);
                comandoActualizar.Parameters.AddWithValue("@total_reserva", totalReserva);
                comandoActualizar.Parameters.AddWithValue("@saldo_pendiente", saldoPendiente);
                comandoActualizar.Parameters.AddWithValue("@observaciones",
                    string.IsNullOrWhiteSpace(modelo.Observaciones)
                        ? DBNull.Value
                        : modelo.Observaciones.Trim());
                comandoActualizar.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Reserva actualizada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar la reserva: " + ex.Message;
                return View(modelo);
            }
        }

    

        // ============================================================
        // CAMBIOS DE ESTADO Y PROCESO DE CHECK-OUT
        // ============================================================

        private CheckoutReservaViewModel? ObtenerCheckoutReserva(
            MySqlConnection conexion,
            int idReserva)
        {
            string consultaReserva = @"
                SELECT
                    r.id_reserva,
                    r.id_reserva_grupo,
                    c.nombre AS cliente,
                    ec.nombre AS empresa_procedencia,
                    h.nombre_proser AS habitacion,
                    r.fecha_entrada,
                    r.fecha_salida,
                    r.fecha_hora_checkin,
                    r.total_reserva,
                    r.saldo_pendiente,
                    CASE
                        WHEN r.id_reserva_grupo IS NULL THEN r.saldo_pendiente
                        ELSE (
                            SELECT COALESCE(SUM(rg.saldo_pendiente), 0)
                            FROM reserva rg
                            WHERE rg.id_reserva_grupo = r.id_reserva_grupo
                              AND rg.estado <> 'cancelada'
                        )
                    END AS saldo_pendiente_grupo,
                    CASE
                        WHEN r.id_reserva_grupo IS NULL THEN 1
                        WHEN NOT EXISTS (
                            SELECT 1
                            FROM reserva siguiente
                            WHERE siguiente.id_reserva_grupo = r.id_reserva_grupo
                              AND siguiente.estado <> 'cancelada'
                              AND (
                                  siguiente.fecha_entrada > r.fecha_entrada
                                  OR (
                                      siguiente.fecha_entrada = r.fecha_entrada
                                      AND siguiente.id_reserva > r.id_reserva
                                  )
                              )
                        ) THEN 1
                        ELSE 0
                    END AS es_ultima_estadia_grupo,
                    r.estado,
                    r.observaciones,
                    rf.requiere_factura,
                    COALESCE(rf.estado_facturacion, 'sin_definir') AS estado_facturacion
                FROM reserva r
                INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                LEFT JOIN cliente_detalle cd ON c.id_clipro = cd.id_clipro
                LEFT JOIN empresa_cliente ec ON cd.id_empresa_cliente = ec.id_empresa_cliente
                INNER JOIN proser h ON r.id_habitacion = h.id_proser
                LEFT JOIN reserva_facturacion rf ON rf.id_reserva = r.id_reserva
                WHERE r.id_reserva = @id_reserva
                LIMIT 1;";

            CheckoutReservaViewModel? modelo = null;

            using (var comando = new MySqlCommand(consultaReserva, conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    return null;
                }

                modelo = new CheckoutReservaViewModel
                {
                    IdReserva = Convert.ToInt32(lector["id_reserva"]),
                    IdReservaGrupo = lector["id_reserva_grupo"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(lector["id_reserva_grupo"]),
                    Cliente = lector["cliente"]?.ToString() ?? "",
                    EmpresaProcedencia = lector["empresa_procedencia"] == DBNull.Value
                        ? null
                        : lector["empresa_procedencia"]?.ToString(),
                    Habitacion = lector["habitacion"]?.ToString() ?? "",
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    FechaHoraCheckIn = lector["fecha_hora_checkin"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(lector["fecha_hora_checkin"]),
                    TotalReserva = Convert.ToDecimal(lector["total_reserva"]),
                    SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]),
                    SaldoPendienteGrupo = Convert.ToDecimal(lector["saldo_pendiente_grupo"]),
                    EsUltimaEstadiaGrupo = Convert.ToInt32(lector["es_ultima_estadia_grupo"]) == 1,
                    Estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "",
                    Observaciones = lector["observaciones"]?.ToString() ?? "",
                    EsAdministrador = EsAdministradorSesion(),
                    RequiereFacturaRegistrada = lector["requiere_factura"] == DBNull.Value
                        ? null
                        : Convert.ToBoolean(lector["requiere_factura"]),
                    EstadoFacturacion = lector["estado_facturacion"]?.ToString() ?? "sin_definir"
                };
            }

            string consultaMovimientos = @"
                SELECT
                    m.id_movimiento,
                    tm.nombre_tipomov AS tipo,
                    fp.nombre_forma AS forma_pago,
                    m.recargo_tarjeta,
                    m.fecha_hora,
                    m.estado,
                    m.observaciones,
                    COALESCE(SUM(d.subtotal), 0) AS monto,
                    COALESCE(
                        GROUP_CONCAT(
                            DISTINCT COALESCE(
                                NULLIF(d.descripcion, ''),
                                p.nombre_proser,
                                tm.nombre_tipomov
                            )
                            SEPARATOR ', '
                        ),
                        tm.nombre_tipomov
                    ) AS descripcion
                FROM movimiento m
                INNER JOIN tipo_movimiento tm
                    ON m.id_tipomov = tm.id_tipomov
                INNER JOIN forma_pago fp
                    ON m.id_formapago = fp.id_formapago
                LEFT JOIN detalle d
                    ON m.id_movimiento = d.id_movimiento
                LEFT JOIN proser p
                    ON d.id_proser = p.id_proser
                WHERE m.id_reserva = @id_reserva
                   OR (
                        @id_reserva_grupo IS NOT NULL
                        AND (
                            m.id_reserva_grupo = @id_reserva_grupo
                            OR m.id_reserva IN (
                                SELECT rh.id_reserva
                                FROM reserva rh
                                WHERE rh.id_reserva_grupo = @id_reserva_grupo
                            )
                        )
                   )
                GROUP BY
                    m.id_movimiento,
                    tm.nombre_tipomov,
                    fp.nombre_forma,
                    m.recargo_tarjeta,
                    m.fecha_hora,
                    m.estado,
                    m.observaciones
                ORDER BY m.fecha_hora, m.id_movimiento;";

            using (var comando = new MySqlCommand(consultaMovimientos, conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                comando.Parameters.AddWithValue(
                    "@id_reserva_grupo",
                    modelo.IdReservaGrupo.HasValue ? modelo.IdReservaGrupo.Value : DBNull.Value);

                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    string tipo = lector["tipo"]?.ToString() ?? "";

                    modelo.Movimientos.Add(new MovimientoCuentaViewModel
                    {
                        IdMovimiento = Convert.ToInt32(lector["id_movimiento"]),
                        Tipo = tipo,
                        Descripcion = lector["descripcion"]?.ToString() ?? "",
                        FormaPago = lector["forma_pago"]?.ToString() ?? "",
                        Monto = Convert.ToDecimal(lector["monto"]),
                        RecargoTarjeta = Convert.ToDecimal(lector["recargo_tarjeta"]),
                        FechaHora = Convert.ToDateTime(lector["fecha_hora"]),
                        Estado = lector["estado"]?.ToString() ?? "",
                        Observaciones = lector["observaciones"]?.ToString() ?? "",
                        EsAbono = tipo.Trim().ToLower() == "abono"
                    });
                }
            }

            return modelo;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Checkout(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                CheckoutReservaViewModel? modelo = ObtenerCheckoutReserva(conexion, id);

                if (modelo == null)
                {
                    TempData["Mensaje"] = "No se encontró la reservación solicitada.";
                    return RedirectToAction("Index");
                }

                if (modelo.Estado != "en_checkout")
                {
                    TempData["Mensaje"] = "La reservación no se encuentra en proceso de check-out.";
                    return RedirectToAction("Index");
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo cargar el check-out: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult IniciarCheckout(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    UPDATE reserva
                    SET estado = 'en_checkout'
                    WHERE id_reserva = @id
                      AND estado = 'en_curso';";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                if (comando.ExecuteNonQuery() == 0)
                {
                    TempData["Mensaje"] = "Solo las reservaciones en curso pueden iniciar el check-out.";
                    return RedirectToAction("Index");
                }

                TempData["Exito"] = "La reservación pasó a check-out correctamente.";
                return RedirectToAction("Index", new { vista = "en_checkout" });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al iniciar el check-out: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckIn(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var transaccion = conexion.BeginTransaction();

                string consulta = @"
                    SELECT id_reserva, id_habitacion, fecha_entrada, estado
                    FROM reserva
                    WHERE id_reserva = @id
                    LIMIT 1
                    FOR UPDATE;";

                int idHabitacion;
                DateTime fechaEntrada;
                string estado;

                using (var cmd = new MySqlCommand(consulta, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Reserva no encontrada.";
                        return RedirectToAction("Index");
                    }

                    estado = reader["estado"]?.ToString()?.Trim().ToLower() ?? "";
                    idHabitacion = Convert.ToInt32(reader["id_habitacion"]);
                    fechaEntrada = Convert.ToDateTime(reader["fecha_entrada"]).Date;
                }

                if (estado != "pendiente")
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Solo las reservas pendientes de ingreso pueden realizar check-in.";
                    return RedirectToAction("Index");
                }

                if (fechaEntrada != DateTime.Today)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = fechaEntrada > DateTime.Today
                        ? "El check-in solo puede realizarse en la fecha de entrada programada."
                        : "La fecha de entrada ya pasó. Cancele esta reservación y registre una nueva si el huésped aún desea hospedarse.";
                    return RedirectToAction("Index");
                }

                string updateReserva = @"
                    UPDATE reserva
                    SET estado = 'en_curso',
                        fecha_hora_checkin = CURRENT_TIMESTAMP
                    WHERE id_reserva = @id;";

                using (var cmd = new MySqlCommand(updateReserva, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }

                int idEstadoOcupada = ObtenerIdEstadoHabitacion(conexion, "ocupada");

                string updateHabitacion = @"
                    UPDATE proser
                    SET id_tipoestado = @estado
                    WHERE id_proser = @id;";

                using (var cmd = new MySqlCommand(updateHabitacion, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@estado", idEstadoOcupada);
                    cmd.Parameters.AddWithValue("@id", idHabitacion);
                    cmd.ExecuteNonQuery();
                }

                transaccion.Commit();

                TempData["Exito"] = "Check-in realizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al realizar el check-in: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegresarEnCurso(int id)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    UPDATE reserva
                    SET estado = 'en_curso'
                    WHERE id_reserva = @id
                      AND estado = 'en_checkout';";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                if (comando.ExecuteNonQuery() == 0)
                {
                    TempData["Mensaje"] = "No se pudo regresar la reservación a la estadía.";
                    return RedirectToAction("Index");
                }

                TempData["Exito"] = "El check-out fue cancelado y la estadía volvió a estar en curso.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cancelar el check-out: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult FinalizarEstadia(
            int id,
            bool autorizarSaldoPendiente = false,
            string? observacionSalida = null,
            bool? requiereFactura = null)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                int? idGrupoParaBloqueo;

                using (var comandoGrupo = new MySqlCommand(@"
                    SELECT id_reserva_grupo
                    FROM reserva
                    WHERE id_reserva = @id
                    LIMIT 1;", conexion, transaccion))
                {
                    comandoGrupo.Parameters.AddWithValue("@id", id);
                    object? resultadoGrupo = comandoGrupo.ExecuteScalar();

                    if (resultadoGrupo == null)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reservación.";
                        return RedirectToAction("Index");
                    }

                    idGrupoParaBloqueo = resultadoGrupo == DBNull.Value
                        ? null
                        : Convert.ToInt32(resultadoGrupo);
                }

                if (idGrupoParaBloqueo.HasValue)
                {
                    using (var comandoGrupo = new MySqlCommand(@"
                        SELECT id_reserva_grupo
                        FROM reserva_grupo
                        WHERE id_reserva_grupo = @id_reserva_grupo
                        FOR UPDATE;", conexion, transaccion))
                    {
                        comandoGrupo.Parameters.AddWithValue("@id_reserva_grupo", idGrupoParaBloqueo.Value);

                        if (comandoGrupo.ExecuteScalar() == null)
                        {
                            transaccion.Rollback();
                            TempData["Mensaje"] = "No se encontró la reserva agrupada.";
                            return RedirectToAction("Index");
                        }
                    }

                    using var comandoReservasGrupo = new MySqlCommand(@"
                        SELECT id_reserva
                        FROM reserva
                        WHERE id_reserva_grupo = @id_reserva_grupo
                        ORDER BY fecha_entrada, id_reserva
                        FOR UPDATE;", conexion, transaccion);
                    comandoReservasGrupo.Parameters.AddWithValue("@id_reserva_grupo", idGrupoParaBloqueo.Value);
                    using var lectorReservasGrupo = comandoReservasGrupo.ExecuteReader();

                    while (lectorReservasGrupo.Read())
                    {
                        // Consumir todas las filas mantiene bloqueadas las estadías del grupo.
                    }
                }

                string consulta = @"
                    SELECT
                        r.id_habitacion,
                        r.id_reserva_grupo,
                        r.saldo_pendiente,
                        r.estado,
                        CASE
                            WHEN r.id_reserva_grupo IS NULL THEN r.saldo_pendiente
                            ELSE (
                                SELECT COALESCE(SUM(rg.saldo_pendiente), 0)
                                FROM reserva rg
                                WHERE rg.id_reserva_grupo = r.id_reserva_grupo
                                  AND rg.estado <> 'cancelada'
                            )
                        END AS saldo_pendiente_grupo,
                        CASE
                            WHEN r.id_reserva_grupo IS NULL THEN 1
                            WHEN NOT EXISTS (
                                SELECT 1
                                FROM reserva siguiente
                                WHERE siguiente.id_reserva_grupo = r.id_reserva_grupo
                                  AND siguiente.estado <> 'cancelada'
                                  AND (
                                      siguiente.fecha_entrada > r.fecha_entrada
                                      OR (
                                          siguiente.fecha_entrada = r.fecha_entrada
                                          AND siguiente.id_reserva > r.id_reserva
                                      )
                                  )
                            ) THEN 1
                            ELSE 0
                        END AS es_ultima_estadia_grupo
                    FROM reserva r
                    WHERE r.id_reserva = @id
                    LIMIT 1
                    FOR UPDATE;";

                int idHabitacion;
                decimal saldoPendiente;
                decimal saldoPendienteGrupo;
                int? idReservaGrupo;
                bool esUltimaEstadiaGrupo;
                string estado;

                using (var comando = new MySqlCommand(consulta, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id", id);

                    using var lector = comando.ExecuteReader();

                    if (!lector.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reservación.";
                        return RedirectToAction("Index");
                    }

                    idHabitacion = Convert.ToInt32(lector["id_habitacion"]);
                    idReservaGrupo = lector["id_reserva_grupo"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(lector["id_reserva_grupo"]);
                    saldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]);
                    saldoPendienteGrupo = Convert.ToDecimal(lector["saldo_pendiente_grupo"]);
                    esUltimaEstadiaGrupo = Convert.ToInt32(lector["es_ultima_estadia_grupo"]) == 1;
                    estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";
                }

                if (estado != "en_checkout")
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La reservación debe encontrarse en check-out para finalizarla.";
                    return RedirectToAction("Index");
                }

                if (!requiereFactura.HasValue)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Debe indicar expresamente si el huésped solicitó factura.";
                    return RedirectToAction("Checkout", new { id });
                }

                bool esEstadiaIntermediaAgrupada = idReservaGrupo.HasValue && !esUltimaEstadiaGrupo;
                decimal saldoQueDebeValidarse = idReservaGrupo.HasValue
                    ? saldoPendienteGrupo
                    : saldoPendiente;

                if (idReservaGrupo.HasValue && esUltimaEstadiaGrupo)
                {
                    using var comandoPendientes = new MySqlCommand(@"
                        SELECT COUNT(*)
                        FROM reserva
                        WHERE id_reserva_grupo = @id_reserva_grupo
                          AND id_reserva <> @id_reserva
                          AND estado NOT IN ('finalizada', 'cancelada');", conexion, transaccion);
                    comandoPendientes.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo.Value);
                    comandoPendientes.Parameters.AddWithValue("@id_reserva", id);

                    if (Convert.ToInt32(comandoPendientes.ExecuteScalar()) > 0)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Antes de cerrar la última estadía deben finalizarse o cancelarse las fechas anteriores del grupo.";
                        return RedirectToAction("Checkout", new { id });
                    }
                }

                if (!esEstadiaIntermediaAgrupada && saldoQueDebeValidarse > 0)
                {
                    if (!EsAdministradorSesion() || !autorizarSaldoPendiente)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = idReservaGrupo.HasValue
                            ? "La cuenta agrupada todavía tiene saldo pendiente. Solo administración puede autorizar el cierre final con deuda."
                            : "La cuenta todavía tiene saldo pendiente. Solo administración puede autorizar esta salida.";
                        return RedirectToAction("Checkout", new { id });
                    }

                    if (string.IsNullOrWhiteSpace(observacionSalida))
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Debe explicar por qué la estadía finalizará con saldo pendiente.";
                        return RedirectToAction("Checkout", new { id });
                    }

                    if (observacionSalida.Trim().Length > 255)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "La explicación no puede superar 255 caracteres.";
                        return RedirectToAction("Checkout", new { id });
                    }
                }

                string actualizarReserva = @"
                    UPDATE reserva
                    SET estado = 'finalizada',
                        fecha_hora_checkout = CURRENT_TIMESTAMP,
                        observaciones = CASE
                            WHEN @observacion IS NULL THEN observaciones
                            WHEN observaciones IS NULL OR TRIM(observaciones) = '' THEN @observacion
                            ELSE CONCAT(observaciones, ' | ', @observacion)
                        END
                    WHERE id_reserva = @id
                      AND estado = 'en_checkout';";

                using (var comando = new MySqlCommand(actualizarReserva, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id", id);
                    comando.Parameters.AddWithValue(
                        "@observacion",
                        string.IsNullOrWhiteSpace(observacionSalida)
                            ? DBNull.Value
                            : observacionSalida.Trim());
                    comando.ExecuteNonQuery();
                }

                int idEstadoLibre = ObtenerIdEstadoHabitacion(conexion, "libre");

                string liberarHabitacion = @"
                    UPDATE proser
                    SET id_tipoestado = @estado
                    WHERE id_proser = @id_habitacion;";

                using (var comando = new MySqlCommand(liberarHabitacion, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@estado", idEstadoLibre);
                    comando.Parameters.AddWithValue("@id_habitacion", idHabitacion);
                    comando.ExecuteNonQuery();
                }

                _facturacionService.RegistrarDecision(
                    conexion,
                    transaccion,
                    id,
                    requiereFactura.Value,
                    ObtenerIdUsuarioSesion(),
                    "checkout",
                    requiereFactura.Value
                        ? "Solicitud registrada al finalizar la estadía."
                        : "El huésped indicó que no requiere factura.");

                transaccion.Commit();

                string mensajeFactura = requiereFactura.Value
                    ? " La solicitud fue enviada a Facturas pendientes."
                    : " Se registró que no requiere factura.";

                TempData["Exito"] = (esEstadiaIntermediaAgrupada && saldoPendienteGrupo > 0
                    ? "Estadía finalizada. La habitación fue liberada y el saldo continúa en la cuenta agrupada."
                    : saldoQueDebeValidarse > 0
                        ? "Estadía finalizada. La habitación fue liberada y la deuda permanece en Cuentas por Cobrar."
                        : "Estadía finalizada y habitación liberada correctamente.") + mensajeFactura;
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al finalizar la estadía: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        private int ObtenerOCrearTipoReembolso(
            MySqlConnection conexion,
            MySqlTransaction transaccion)
        {
            using (var comando = new MySqlCommand(@"
                SELECT id_tipomov
                FROM tipo_movimiento
                WHERE LOWER(nombre_tipomov) = 'reembolso'
                LIMIT 1;", conexion, transaccion))
            {
                object? resultado = comando.ExecuteScalar();
                if (resultado != null)
                {
                    return Convert.ToInt32(resultado);
                }
            }

            using (var comando = new MySqlCommand(@"
                INSERT INTO tipo_movimiento (nombre_tipomov)
                VALUES ('reembolso');", conexion, transaccion))
            {
                comando.ExecuteNonQuery();
                return Convert.ToInt32(comando.LastInsertedId);
            }
        }

        private void RegistrarReembolsoCancelacion(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idTipoReembolso,
            int idCliente,
            int idFormaPago,
            int idReserva,
            int? idReservaGrupo,
            int idMovimientoOriginal,
            decimal monto)
        {
            int idMovimientoReembolso;

            using (var comando = new MySqlCommand(@"
                INSERT INTO movimiento
                (
                    id_usuario,
                    id_clipro,
                    id_tipomov,
                    id_formapago,
                    id_reserva,
                    id_reserva_grupo,
                    fecha_hora,
                    estado,
                    observaciones
                )
                VALUES
                (
                    @id_usuario,
                    @id_clipro,
                    @id_tipomov,
                    @id_formapago,
                    @id_reserva,
                    @id_reserva_grupo,
                    NOW(),
                    'activo',
                    @observaciones
                );", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_usuario", ObtenerIdUsuarioSesion());
                comando.Parameters.AddWithValue("@id_clipro", idCliente);
                comando.Parameters.AddWithValue("@id_tipomov", idTipoReembolso);
                comando.Parameters.AddWithValue("@id_formapago", idFormaPago);
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                comando.Parameters.AddWithValue(
                    "@id_reserva_grupo",
                    idReservaGrupo.HasValue ? idReservaGrupo.Value : DBNull.Value);
                comando.Parameters.AddWithValue(
                    "@observaciones",
                    $"Reembolso por cancelación de la estadía #{idReserva}. Pago original #{idMovimientoOriginal}.");
                comando.ExecuteNonQuery();
                idMovimientoReembolso = Convert.ToInt32(comando.LastInsertedId);
            }

            using var comandoDetalle = new MySqlCommand(@"
                INSERT INTO detalle
                (
                    id_movimiento,
                    id_proser,
                    cantidad,
                    precio_unitario,
                    subtotal,
                    descripcion
                )
                VALUES
                (
                    @id_movimiento,
                    NULL,
                    1,
                    @monto,
                    @monto,
                    @descripcion
                );", conexion, transaccion);
            comandoDetalle.Parameters.AddWithValue("@id_movimiento", idMovimientoReembolso);
            comandoDetalle.Parameters.AddWithValue("@monto", monto);
            comandoDetalle.Parameters.AddWithValue(
                "@descripcion",
                $"Reembolso registrado por cancelación de la estadía #{idReserva}");
            comandoDetalle.ExecuteNonQuery();
        }

        private bool CancelarReservaPendienteAgrupada(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idReserva,
            bool registrarReembolso,
            out string mensaje,
            out decimal montoReembolso)
        {
            montoReembolso = 0;
            int? idReservaGrupo;

            using (var comando = new MySqlCommand(@"
                SELECT id_reserva_grupo
                FROM reserva
                WHERE id_reserva = @id_reserva
                LIMIT 1;", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                object? resultado = comando.ExecuteScalar();

                if (resultado == null)
                {
                    mensaje = "No se encontró la reservación.";
                    return false;
                }

                idReservaGrupo = resultado == DBNull.Value ? null : Convert.ToInt32(resultado);
            }

            if (!idReservaGrupo.HasValue)
            {
                var abonos = new List<(int IdMovimiento, int IdCliente, int IdFormaPago, decimal Monto)>();

                using (var comando = new MySqlCommand(@"
                    SELECT
                        m.id_movimiento,
                        m.id_clipro,
                        m.id_formapago,
                        COALESCE(SUM(d.subtotal), 0) AS monto
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                    LEFT JOIN detalle d ON m.id_movimiento = d.id_movimiento
                    WHERE m.id_reserva = @id_reserva
                      AND m.estado = 'activo'
                      AND LOWER(tm.nombre_tipomov) = 'abono'
                    GROUP BY m.id_movimiento, m.id_clipro, m.id_formapago
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    using var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        abonos.Add((
                            Convert.ToInt32(lector["id_movimiento"]),
                            Convert.ToInt32(lector["id_clipro"]),
                            Convert.ToInt32(lector["id_formapago"]),
                            Convert.ToDecimal(lector["monto"])));
                    }
                }

                montoReembolso = abonos.Sum(abono => abono.Monto);
                if (montoReembolso > 0 && !registrarReembolso)
                {
                    mensaje = $"La estadía tiene Q {montoReembolso:N2} pagados. Para cancelarla debe confirmar el registro del reembolso.";
                    return false;
                }

                if (montoReembolso > 0)
                {
                    int idTipoReembolsoIndividual = ObtenerOCrearTipoReembolso(conexion, transaccion);
                    foreach (var abono in abonos.Where(abono => abono.Monto > 0))
                    {
                        RegistrarReembolsoCancelacion(
                            conexion,
                            transaccion,
                            idTipoReembolsoIndividual,
                            abono.IdCliente,
                            abono.IdFormaPago,
                            idReserva,
                            null,
                            abono.IdMovimiento,
                            abono.Monto);
                    }
                }

                using var comandoCancelar = new MySqlCommand(@"
                    UPDATE reserva
                    SET estado = 'cancelada',
                        saldo_pendiente = 0
                    WHERE id_reserva = @id_reserva
                      AND estado = 'pendiente';", conexion, transaccion);
                comandoCancelar.Parameters.AddWithValue("@id_reserva", idReserva);

                if (comandoCancelar.ExecuteNonQuery() == 0)
                {
                    mensaje = "Solo se pueden cancelar reservaciones pendientes de ingreso.";
                    return false;
                }

                mensaje = montoReembolso > 0
                    ? $"Reserva cancelada y reembolso de Q {montoReembolso:N2} registrado correctamente."
                    : "Reserva cancelada correctamente.";
                return true;
            }

            using (var comando = new MySqlCommand(@"
                SELECT id_reserva_grupo
                FROM reserva_grupo
                WHERE id_reserva_grupo = @id_reserva_grupo
                FOR UPDATE;", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo.Value);
                comando.ExecuteScalar();
            }

            var reservasGrupo = new List<(int IdReserva, decimal Saldo, string Estado)>();

            using (var comando = new MySqlCommand(@"
                SELECT id_reserva, saldo_pendiente, estado
                FROM reserva
                WHERE id_reserva_grupo = @id_reserva_grupo
                ORDER BY fecha_entrada, id_reserva
                FOR UPDATE;", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo.Value);
                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    reservasGrupo.Add((
                        Convert.ToInt32(lector["id_reserva"]),
                        Convert.ToDecimal(lector["saldo_pendiente"]),
                        lector["estado"]?.ToString()?.Trim().ToLower() ?? ""));
                }
            }

            var reservaCancelada = reservasGrupo.FirstOrDefault(reserva => reserva.IdReserva == idReserva);

            if (reservaCancelada.IdReserva == 0 || reservaCancelada.Estado != "pendiente")
            {
                mensaje = "Solo se pueden cancelar reservaciones pendientes de ingreso.";
                return false;
            }

            var aplicaciones = new List<(int IdMovimiento, int IdCliente, int IdFormaPago, decimal Monto)>();

            using (var comando = new MySqlCommand(@"
                SELECT a.id_movimiento, m.id_clipro, m.id_formapago, a.monto
                FROM movimiento_reserva_aplicacion a
                INNER JOIN movimiento m ON a.id_movimiento = m.id_movimiento
                WHERE a.id_reserva = @id_reserva
                  AND m.estado = 'activo'
                ORDER BY a.id_movimiento
                FOR UPDATE;", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    aplicaciones.Add((
                        Convert.ToInt32(lector["id_movimiento"]),
                        Convert.ToInt32(lector["id_clipro"]),
                        Convert.ToInt32(lector["id_formapago"]),
                        Convert.ToDecimal(lector["monto"])));
                }
            }

            decimal totalAplicado = aplicaciones.Sum(aplicacion => aplicacion.Monto);
            var destinos = reservasGrupo
                .Where(reserva =>
                    reserva.IdReserva != idReserva &&
                    reserva.Estado != "cancelada" &&
                    reserva.Saldo > 0)
                .Select(reserva => (reserva.IdReserva, reserva.Saldo))
                .ToList();
            decimal capacidadDisponible = destinos.Sum(destino => destino.Saldo);
            montoReembolso = Math.Max(0, totalAplicado - capacidadDisponible);

            if (montoReembolso > 0 && !registrarReembolso)
            {
                mensaje = $"La estadía tiene Q {montoReembolso:N2} pagados que no pueden trasladarse a otra fecha. Para cancelarla debe confirmar el registro del reembolso.";
                return false;
            }

            int indiceDestino = 0;
            int? idTipoReembolso = montoReembolso > 0
                ? ObtenerOCrearTipoReembolso(conexion, transaccion)
                : null;

            foreach (var aplicacion in aplicaciones)
            {
                using (var comandoEliminar = new MySqlCommand(@"
                    DELETE FROM movimiento_reserva_aplicacion
                    WHERE id_movimiento = @id_movimiento
                      AND id_reserva = @id_reserva;", conexion, transaccion))
                {
                    comandoEliminar.Parameters.AddWithValue("@id_movimiento", aplicacion.IdMovimiento);
                    comandoEliminar.Parameters.AddWithValue("@id_reserva", idReserva);
                    comandoEliminar.ExecuteNonQuery();
                }

                decimal restante = aplicacion.Monto;

                while (restante > 0 && indiceDestino < destinos.Count)
                {
                    var destino = destinos[indiceDestino];

                    if (destino.Saldo <= 0)
                    {
                        indiceDestino++;
                        continue;
                    }

                    decimal trasladado = Math.Min(restante, destino.Saldo);

                    using (var comandoActualizar = new MySqlCommand(@"
                        UPDATE reserva
                        SET saldo_pendiente = saldo_pendiente - @monto
                        WHERE id_reserva = @id_reserva;", conexion, transaccion))
                    {
                        comandoActualizar.Parameters.AddWithValue("@monto", trasladado);
                        comandoActualizar.Parameters.AddWithValue("@id_reserva", destino.IdReserva);
                        comandoActualizar.ExecuteNonQuery();
                    }

                    using (var comandoAplicacion = new MySqlCommand(@"
                        INSERT INTO movimiento_reserva_aplicacion
                            (id_movimiento, id_reserva, monto)
                        VALUES
                            (@id_movimiento, @id_reserva, @monto)
                        ON DUPLICATE KEY UPDATE monto = monto + VALUES(monto);", conexion, transaccion))
                    {
                        comandoAplicacion.Parameters.AddWithValue("@id_movimiento", aplicacion.IdMovimiento);
                        comandoAplicacion.Parameters.AddWithValue("@id_reserva", destino.IdReserva);
                        comandoAplicacion.Parameters.AddWithValue("@monto", trasladado);
                        comandoAplicacion.ExecuteNonQuery();
                    }

                    restante -= trasladado;
                    destino.Saldo -= trasladado;
                    destinos[indiceDestino] = destino;

                    if (destino.Saldo <= 0)
                    {
                        indiceDestino++;
                    }
                }

                if (restante > 0)
                {
                    RegistrarReembolsoCancelacion(
                        conexion,
                        transaccion,
                        idTipoReembolso!.Value,
                        aplicacion.IdCliente,
                        aplicacion.IdFormaPago,
                        idReserva,
                        idReservaGrupo,
                        aplicacion.IdMovimiento,
                        restante);
                }
            }

            using (var comando = new MySqlCommand(@"
                UPDATE reserva
                SET estado = 'cancelada',
                    saldo_pendiente = 0
                WHERE id_reserva = @id_reserva
                  AND estado = 'pendiente';", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);

                if (comando.ExecuteNonQuery() == 0)
                {
                    mensaje = "La reservación cambió de estado antes de completar la cancelación.";
                    return false;
                }
            }

            mensaje = montoReembolso > 0
                ? $"Reserva cancelada y reembolso de Q {montoReembolso:N2} registrado correctamente."
                : totalAplicado > 0
                    ? "Reserva cancelada y pago trasladado a las siguientes estadías pendientes."
                    : "Reserva cancelada correctamente.";
            return true;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Cancelar(int id, bool registrarReembolso = false)
        {
            IActionResult? acceso = ValidarAccesoSoloAdministrativo();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                if (!CancelarReservaPendienteAgrupada(
                    conexion,
                    transaccion,
                    id,
                    registrarReembolso,
                    out string mensaje,
                    out decimal montoReembolso))
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = mensaje;

                    if (montoReembolso > 0 && !registrarReembolso)
                    {
                        TempData["ReservaReembolsoPendiente"] = id;
                        TempData["MontoReembolsoPendiente"] = montoReembolso.ToString(
                            System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    transaccion.Commit();
                    TempData["Exito"] = mensaje;
                }
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cancelar la reserva: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
