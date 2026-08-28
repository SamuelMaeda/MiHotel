// ================================================
// CONTROLADOR DE CUENTAS POR COBRAR
// Administra el listado de cuentas pendientes
// agrupadas por estadía y las cuentas independientes.
// ================================================

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Services;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class CuentasPorCobrarController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly FacturacionService _facturacionService;

        private const int RegistrosPorPagina = 20;

        public CuentasPorCobrarController(ConexionBD conexionBD, FacturacionService facturacionService)
        {
            _conexionBD = conexionBD;
            _facturacionService = facturacionService;
        }

        // ================================================
        // VALIDAR SI EXISTE UNA SESIÓN ADMINISTRATIVA
        // ================================================

        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");

            return !string.IsNullOrWhiteSpace(idUsuario);
        }

        // ================================================
        // VALIDAR SI EL USUARIO ES ADMINISTRADOR
        // ================================================

        private bool EsAdministrador()
        {
            string nombreRol = HttpContext.Session
                .GetString("NombreRol")?
                .Trim()
                .ToLower() ?? "";

            return nombreRol == "admin";
        }

        private static bool EsFormaPagoTarjeta(string nombreFormaPago)
        {
            return nombreFormaPago.Contains("tarjeta", StringComparison.OrdinalIgnoreCase);
        }

        private decimal ObtenerRecargoTarjeta(
            MySqlConnection conexion,
            MySqlTransaction? transaccion = null)
        {
            using var comando = new MySqlCommand(@"
                SELECT recargo_tarjeta
                FROM configuracion_sistema
                WHERE id_configuracion = 1
                LIMIT 1;", conexion, transaccion);

            object? resultado = comando.ExecuteScalar();
            return resultado == null || resultado == DBNull.Value
                ? 25m
                : Convert.ToDecimal(resultado);
        }

        private string? ObtenerNombreFormaPago(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idFormaPago)
        {
            using var comando = new MySqlCommand(@"
                SELECT nombre_forma
                FROM forma_pago
                WHERE id_formapago = @id_formapago
                  AND LOWER(nombre_forma) <> 'credito'
                LIMIT 1;", conexion, transaccion);

            comando.Parameters.AddWithValue("@id_formapago", idFormaPago);
            return comando.ExecuteScalar()?.ToString();
        }

        // ================================================
        // VALIDAR PERMISO ASIGNADO AL ROL
        // ================================================

        private bool TienePermiso(string nombrePermiso)
        {
            if (!TieneSesionActiva())
            {
                return false;
            }

            if (EsAdministrador())
            {
                return true;
            }

            string? idRolSesion = HttpContext.Session.GetString("IdRol");

            if (string.IsNullOrWhiteSpace(idRolSesion))
            {
                return false;
            }

            if (!int.TryParse(idRolSesion, out int idRol))
            {
                return false;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();

                conexion.Open();

                string consulta = @"
                    SELECT COUNT(*)
                    FROM rol_permiso rp
                    INNER JOIN permisos p
                        ON rp.id_permiso = p.id_permiso
                    WHERE rp.id_rol = @id_rol
                      AND p.nombre_permiso = @nombre_permiso
                      AND p.estado = 1;";

                using var comando = new MySqlCommand(consulta, conexion);

                comando.Parameters.AddWithValue("@id_rol", idRol);
                comando.Parameters.AddWithValue(
                    "@nombre_permiso",
                    nombrePermiso
                );

                int cantidad = Convert.ToInt32(
                    comando.ExecuteScalar()
                );

                return cantidad > 0;
            }
            catch
            {
                return false;
            }
        }

        // ================================================
        // VALIDAR ACCESO AL MÓDULO
        // ================================================

        private IActionResult? ValidarAcceso()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            if (!TienePermiso("gestionar_cxc"))
            {
                TempData["Mensaje"] =
                    "No tiene permiso para acceder a Cuentas por Cobrar.";

                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        // ================================================
        // OBTENER COLUMNA SEGURA PARA ORDENAMIENTO
        // ================================================

        private string ObtenerColumnaOrden(string ordenarPor)
        {
            return ordenarPor switch
            {
                "cliente" => "cuenta.cliente",
                "empresa" => "cuenta.empresa_procedencia",
                "habitacion" => "cuenta.habitacion",
                "fecha_entrada" => "cuenta.fecha_entrada",
                "fecha_salida" => "cuenta.fecha_salida",
                "hora_checkin" => "cuenta.fecha_hora_checkin",
                "hora_checkout" => "cuenta.fecha_hora_checkout",
                "total" => "cuenta.total_reserva",
                "saldo" => "cuenta.saldo",
                _ => "cuenta.fecha_entrada"
            };
        }

        // ================================================
        // LISTADO PRINCIPAL POR ESTADÍAS
        // ================================================

        [HttpGet]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None
        )]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "fecha_entrada",
            string direccion = "desc",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();

            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaCuentas = new DataTable();

            busqueda = busqueda?.Trim() ?? "";
            ordenarPor = ordenarPor?.Trim().ToLower() ?? "fecha_entrada";

            string direccionNormalizada =
                direccion?.Trim().ToLower() == "asc"
                    ? "asc"
                    : "desc";

            if (pagina < 1)
            {
                pagina = 1;
            }

            string columnaOrden = ObtenerColumnaOrden(ordenarPor);

            ViewBag.Busqueda = busqueda;
            ViewBag.OrdenarPor = ordenarPor;
            ViewBag.Direccion = direccionNormalizada;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = 1;
            ViewBag.TotalRegistros = 0;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();

                conexion.Open();

                string consultaBase = @"
                    SELECT
                        'estadia' AS tipo_cuenta,
                        r.id_reserva,
                        r.id_reserva_grupo,
                        r.id_reserva AS id_movimiento_referencia,
                        r.id_clipro,
                        c.nombre AS cliente,
                        ec.nombre AS empresa_procedencia,
                        h.codigo AS habitacion,
                        r.fecha_entrada,
                        r.fecha_salida,
                        r.fecha_hora_checkin,
                        r.fecha_hora_checkout,
                        r.total_reserva,
                        r.fecha_reserva AS fecha_cuenta,
                        r.saldo_pendiente AS saldo,
                        r.estado,
                        r.observaciones,
                        (
                            SELECT COUNT(*)
                            FROM movimiento m2
                            WHERE m2.id_reserva = r.id_reserva
                        ) AS cantidad_movimientos
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    LEFT JOIN cliente_detalle cd ON c.id_clipro = cd.id_clipro
                    LEFT JOIN empresa_cliente ec ON cd.id_empresa_cliente = ec.id_empresa_cliente
                    INNER JOIN proser h ON r.id_habitacion = h.id_proser
                    WHERE r.saldo_pendiente > 0
                      AND r.estado <> 'cancelada'
                    ";

                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    condicionBusqueda = @"
                        AND (
                            cuenta.cliente LIKE @busqueda
                            OR cuenta.empresa_procedencia LIKE @busqueda
                            OR cuenta.habitacion LIKE @busqueda
                            OR CAST(
                                cuenta.id_reserva AS CHAR
                            ) LIKE @busqueda
                            OR CAST(
                                cuenta.id_reserva_grupo AS CHAR
                            ) LIKE @busqueda
                            OR CAST(
                                cuenta.id_movimiento_referencia AS CHAR
                            ) LIKE @busqueda
                            OR cuenta.observaciones LIKE @busqueda
                        )";
                }

                // ================================================
                // CONTAR CUENTAS O ESTADÍAS, NO FILAS DE DETALLE
                // ================================================

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM (
                        {consultaBase}
                    ) AS cuenta
                    WHERE 1 = 1
                    {condicionBusqueda};";

                using var comandoConteo = new MySqlCommand(
                    consultaConteo,
                    conexion
                );

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comandoConteo.Parameters.AddWithValue(
                        "@busqueda",
                        "%" + busqueda + "%"
                    );
                }

                int totalRegistros = Convert.ToInt32(
                    comandoConteo.ExecuteScalar()
                );

                int totalPaginas = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (double)totalRegistros / RegistrosPorPagina
                    )
                );

                if (pagina > totalPaginas)
                {
                    pagina = totalPaginas;
                }

                int offset = (pagina - 1) * RegistrosPorPagina;

                // ================================================
                // CONSULTAR LAS CUENTAS DE LA PÁGINA ACTUAL
                // ================================================

                string consulta = $@"
                    SELECT
                        cuenta.tipo_cuenta,
                        cuenta.id_reserva,
                        cuenta.id_reserva_grupo,
                        cuenta.id_movimiento_referencia,
                        cuenta.id_clipro,
                        cuenta.cliente,
                        cuenta.empresa_procedencia,
                        cuenta.habitacion,
                        cuenta.fecha_entrada,
                        cuenta.fecha_salida,
                        cuenta.fecha_hora_checkin,
                        cuenta.fecha_hora_checkout,
                        cuenta.total_reserva,
                        cuenta.fecha_cuenta,
                        cuenta.saldo,
                        cuenta.estado,
                        cuenta.observaciones,
                        cuenta.cantidad_movimientos
                    FROM (
                        {consultaBase}
                    ) AS cuenta
                    WHERE 1 = 1
                    {condicionBusqueda}
                    ORDER BY
                        {columnaOrden} {direccionNormalizada},
                        cuenta.fecha_cuenta DESC,
                        cuenta.id_movimiento_referencia DESC
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(
                    consulta,
                    conexion
                );

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue(
                        "@busqueda",
                        "%" + busqueda + "%"
                    );
                }

                comando.Parameters.AddWithValue(
                    "@limite",
                    RegistrosPorPagina
                );

                comando.Parameters.AddWithValue(
                    "@offset",
                    offset
                );

                using var adaptador = new MySqlDataAdapter(comando);

                adaptador.Fill(tablaCuentas);

                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje =
                    "Ocurrió un error al cargar las cuentas por cobrar: "
                    + ex.Message;
            }

            return View(tablaCuentas);
        }

        private IActionResult? ValidarCobro()
        {
            IActionResult? acceso = ValidarAcceso();

            if (acceso != null)
            {
                return acceso;
            }

            if (!TienePermiso("cobrar_cuenta"))
            {
                TempData["Mensaje"] = "No tiene permiso para registrar o corregir abonos.";
                return RedirectToAction("Index");
            }

            return null;
        }

        private int ObtenerIdUsuarioSesion()
        {
            string? idUsuarioSesion = HttpContext.Session.GetString("IdUsuario");

            if (!int.TryParse(idUsuarioSesion, out int idUsuario))
            {
                throw new InvalidOperationException("No se pudo identificar el usuario de la sesión.");
            }

            return idUsuario;
        }

        private CuentaPorCobrarDetalleViewModel? ObtenerDetalleCuenta(
            MySqlConnection conexion,
            int idReserva)
        {
            string consultaReserva = @"
                SELECT
                    r.id_reserva,
                    r.id_reserva_grupo,
                    c.nombre AS cliente,
                    h.nombre_proser AS habitacion,
                    r.fecha_entrada,
                    r.fecha_salida,
                    r.estado,
                    r.saldo_pendiente
                FROM reserva r
                INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                INNER JOIN proser h ON r.id_habitacion = h.id_proser
                WHERE r.id_reserva = @id_reserva
                LIMIT 1;";

            CuentaPorCobrarDetalleViewModel? modelo;

            using (var comando = new MySqlCommand(consultaReserva, conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    return null;
                }

                modelo = new CuentaPorCobrarDetalleViewModel
                {
                    IdReserva = Convert.ToInt32(lector["id_reserva"]),
                    IdReservaGrupo = lector["id_reserva_grupo"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(lector["id_reserva_grupo"]),
                    Cliente = lector["cliente"]?.ToString() ?? "",
                    Habitacion = lector["habitacion"]?.ToString() ?? "",
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    EstadoReserva = lector["estado"]?.ToString() ?? "",
                    SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"])
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
                GROUP BY
                    m.id_movimiento,
                    tm.nombre_tipomov,
                    fp.nombre_forma,
                    m.recargo_tarjeta,
                    m.fecha_hora,
                    m.estado,
                    m.observaciones
                ORDER BY m.fecha_hora DESC, m.id_movimiento DESC;";

            using (var comando = new MySqlCommand(consultaMovimientos, conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);

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

        private CuentaPorCobrarDetalleViewModel? ObtenerDetalleCuentaGrupo(
            MySqlConnection conexion,
            int idReservaGrupo,
            int? idReservaRetorno = null)
        {
            const string consultaGrupo = @"
                SELECT
                    rg.id_reserva_grupo,
                    rg.id_clipro,
                    c.nombre AS cliente,
                    GROUP_CONCAT(DISTINCT h.nombre_proser ORDER BY h.nombre_proser SEPARATOR ', ') AS habitacion,
                    MIN(r.fecha_entrada) AS fecha_entrada,
                    MAX(r.fecha_salida) AS fecha_salida,
                    SUM(CASE WHEN r.estado <> 'cancelada' THEN r.saldo_pendiente ELSE 0 END) AS saldo_pendiente
                FROM reserva_grupo rg
                INNER JOIN clipro c ON rg.id_clipro = c.id_clipro
                INNER JOIN reserva r ON r.id_reserva_grupo = rg.id_reserva_grupo
                INNER JOIN proser h ON r.id_habitacion = h.id_proser
                WHERE rg.id_reserva_grupo = @id_reserva_grupo
                GROUP BY rg.id_reserva_grupo, rg.id_clipro, c.nombre
                LIMIT 1;";

            CuentaPorCobrarDetalleViewModel? modelo;

            using (var comando = new MySqlCommand(consultaGrupo, conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    return null;
                }

                modelo = new CuentaPorCobrarDetalleViewModel
                {
                    IdReservaGrupo = Convert.ToInt32(lector["id_reserva_grupo"]),
                    IdReservaRetorno = idReservaRetorno,
                    Cliente = lector["cliente"]?.ToString() ?? "",
                    Habitacion = lector["habitacion"]?.ToString() ?? "",
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    EstadoReserva = "varias_estadias",
                    SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"])
                };
            }

            using (var comando = new MySqlCommand(@"
                SELECT
                    r.id_reserva,
                    h.nombre_proser AS habitacion,
                    r.fecha_entrada,
                    r.fecha_salida,
                    r.total_reserva,
                    r.saldo_pendiente,
                    r.estado
                FROM reserva r
                INNER JOIN proser h ON r.id_habitacion = h.id_proser
                WHERE r.id_reserva_grupo = @id_reserva_grupo
                ORDER BY r.fecha_entrada, r.id_reserva;", conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    modelo.EstadiasAgrupadas.Add(new ReservaGrupoItemViewModel
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
            }

            using (var comando = new MySqlCommand(@"
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
                            DISTINCT COALESCE(NULLIF(d.descripcion, ''), p.nombre_proser, tm.nombre_tipomov)
                            SEPARATOR ', '
                        ),
                        tm.nombre_tipomov
                    ) AS descripcion
                FROM movimiento m
                INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                INNER JOIN forma_pago fp ON m.id_formapago = fp.id_formapago
                LEFT JOIN detalle d ON m.id_movimiento = d.id_movimiento
                LEFT JOIN proser p ON d.id_proser = p.id_proser
                WHERE m.id_reserva_grupo = @id_reserva_grupo
                   OR m.id_reserva IN (
                        SELECT rh.id_reserva
                        FROM reserva rh
                        WHERE rh.id_reserva_grupo = @id_reserva_grupo
                   )
                GROUP BY
                    m.id_movimiento,
                    tm.nombre_tipomov,
                    fp.nombre_forma,
                    m.recargo_tarjeta,
                    m.fecha_hora,
                    m.estado,
                    m.observaciones
                ORDER BY m.fecha_hora DESC, m.id_movimiento DESC;", conexion))
            {
                comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
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

        private void CargarFormasPago(MySqlConnection conexion)
        {
            var formasPago = new DataTable();

            using var adaptador = new MySqlDataAdapter(@"
                SELECT id_formapago, nombre_forma,
                       CASE WHEN LOWER(nombre_forma) LIKE '%tarjeta%' THEN 1 ELSE 0 END AS es_tarjeta
                FROM forma_pago
                WHERE LOWER(nombre_forma) <> 'credito'
                ORDER BY nombre_forma;", conexion);

            adaptador.Fill(formasPago);
            ViewBag.FormasPago = formasPago;
            ViewBag.RecargoTarjeta = ObtenerRecargoTarjeta(conexion);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
        {
            IActionResult? acceso = ValidarAcceso();

            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                CuentaPorCobrarDetalleViewModel? modelo = ObtenerDetalleCuenta(conexion, id);

                if (modelo == null)
                {
                    TempData["Mensaje"] = "No se encontró la cuenta solicitada.";
                    return RedirectToAction("Index");
                }

                if (modelo.IdReservaGrupo.HasValue)
                {
                    return RedirectToAction("DetalleGrupo", new
                    {
                        id = modelo.IdReservaGrupo.Value,
                        idReservaRetorno = id
                    });
                }

                CargarFormasPago(conexion);
                ViewBag.PuedeCobrar = TienePermiso("cobrar_cuenta");

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo cargar la cuenta: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult DetalleGrupo(int id, int? idReservaRetorno = null)
        {
            IActionResult? acceso = ValidarAcceso();

            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                CuentaPorCobrarDetalleViewModel? modelo = ObtenerDetalleCuentaGrupo(conexion, id, idReservaRetorno);

                if (modelo == null)
                {
                    TempData["Mensaje"] = "No se encontró la cuenta agrupada solicitada.";
                    return RedirectToAction("Index");
                }

                CargarFormasPago(conexion);
                ViewBag.PuedeCobrar = TienePermiso("cobrar_cuenta");
                return View("Detalle", modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo cargar la cuenta agrupada: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarAbono(
            int idReserva,
            decimal monto,
            int idFormaPago,
            string? referencia,
            string? observaciones,
            bool solicitarFacturaAlLiquidar = false,
            bool volverCheckout = false)
        {
            IActionResult? acceso = ValidarCobro();

            if (acceso != null)
            {
                return acceso;
            }

            if (monto <= 0)
            {
                TempData["Mensaje"] = "El abono debe ser mayor que Q0.00.";
                return RedirectToAction("Detalle", new { id = idReserva });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                string consultaReserva = @"
                    SELECT id_clipro, saldo_pendiente, estado
                    FROM reserva
                    WHERE id_reserva = @id_reserva
                    LIMIT 1
                    FOR UPDATE;";

                int idCliente;
                decimal saldoPendiente;
                string estadoReserva;

                using (var comando = new MySqlCommand(consultaReserva, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);

                    using var lector = comando.ExecuteReader();

                    if (!lector.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reservación.";
                        return RedirectToAction("Index");
                    }

                    idCliente = Convert.ToInt32(lector["id_clipro"]);
                    saldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]);
                    estadoReserva = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";
                }

                string[] estadosPermitidos = { "pendiente", "en_curso", "en_checkout", "finalizada" };

                if (!estadosPermitidos.Contains(estadoReserva))
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "El estado de la reservación no permite registrar abonos.";
                    return RedirectToAction("Detalle", new { id = idReserva });
                }

                if (monto > saldoPendiente)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "El abono no puede ser mayor que el saldo pendiente.";
                    return RedirectToAction("Detalle", new { id = idReserva });
                }

                if (solicitarFacturaAlLiquidar && monto != saldoPendiente)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Para solicitar la factura desde este pago debe liquidar el saldo completo.";
                    return RedirectToAction("Detalle", new { id = idReserva });
                }

                string? nombreFormaPago = ObtenerNombreFormaPago(conexion, transaccion, idFormaPago);

                if (string.IsNullOrWhiteSpace(nombreFormaPago))
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La forma de pago seleccionada no es válida.";
                    return RedirectToAction("Detalle", new { id = idReserva });
                }

                decimal recargoTarjeta = EsFormaPagoTarjeta(nombreFormaPago)
                    ? ObtenerRecargoTarjeta(conexion, transaccion)
                    : 0m;

                string obtenerTipoAbono = @"
                    SELECT id_tipomov
                    FROM tipo_movimiento
                    WHERE LOWER(nombre_tipomov) = 'abono'
                    LIMIT 1;";

                int idTipoAbono;

                using (var comando = new MySqlCommand(obtenerTipoAbono, conexion, transaccion))
                {
                    object? resultado = comando.ExecuteScalar();

                    if (resultado == null)
                    {
                        throw new InvalidOperationException(
                            "No existe el tipo de movimiento 'abono'. Aplique primero el cambio SQL indicado.");
                    }

                    idTipoAbono = Convert.ToInt32(resultado);
                }

                string observacionCompleta = string.Join(
                    " | ",
                    new[] { referencia?.Trim(), observaciones?.Trim() }
                        .Where(valor => !string.IsNullOrWhiteSpace(valor)));

                if (observacionCompleta.Length > 255)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La referencia y las observaciones no pueden superar 255 caracteres en total.";
                    return RedirectToAction("Detalle", new { id = idReserva });
                }

                string insertarMovimiento = @"
                    INSERT INTO movimiento
                    (
                        id_usuario,
                        id_clipro,
                        id_tipomov,
                        id_formapago,
                        id_reserva,
                        recargo_tarjeta,
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
                        @recargo_tarjeta,
                        NOW(),
                        'activo',
                        @observaciones
                    );";

                int idMovimiento;

                using (var comando = new MySqlCommand(insertarMovimiento, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_usuario", ObtenerIdUsuarioSesion());
                    comando.Parameters.AddWithValue("@id_clipro", idCliente);
                    comando.Parameters.AddWithValue("@id_tipomov", idTipoAbono);
                    comando.Parameters.AddWithValue("@id_formapago", idFormaPago);
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    comando.Parameters.AddWithValue("@recargo_tarjeta", recargoTarjeta);
                    comando.Parameters.AddWithValue(
                        "@observaciones",
                        string.IsNullOrWhiteSpace(observacionCompleta)
                            ? DBNull.Value
                            : observacionCompleta);
                    comando.ExecuteNonQuery();
                    idMovimiento = Convert.ToInt32(comando.LastInsertedId);
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
                        NULL,
                        1,
                        @monto,
                        @monto,
                        @descripcion
                    );";

                using (var comando = new MySqlCommand(insertarDetalle, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);
                    comando.Parameters.AddWithValue("@monto", monto);
                    comando.Parameters.AddWithValue("@descripcion", $"Abono a reservación #{idReserva}");
                    comando.ExecuteNonQuery();
                }

                string actualizarSaldo = @"
                    UPDATE reserva
                    SET saldo_pendiente = saldo_pendiente - @monto
                    WHERE id_reserva = @id_reserva;";

                using (var comando = new MySqlCommand(actualizarSaldo, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@monto", monto);
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    comando.ExecuteNonQuery();
                }

                if (solicitarFacturaAlLiquidar)
                {
                    _facturacionService.RegistrarDecision(
                        conexion,
                        transaccion,
                        idReserva,
                        true,
                        ObtenerIdUsuarioSesion(),
                        "pago_final",
                        "Factura solicitada al liquidar la cuenta después de la estadía.");
                }

                transaccion.Commit();
                string resumenRecargo = recargoTarjeta > 0
                    ? $" Se aplicó un recargo de Q{recargoTarjeta:N2}; total cobrado Q{monto + recargoTarjeta:N2}."
                    : "";
                TempData["Exito"] = solicitarFacturaAlLiquidar
                    ? "Pago registrado y reservación enviada a Facturas pendientes." + resumenRecargo
                    : "Abono registrado correctamente." + resumenRecargo;

                return volverCheckout
                    ? RedirectToAction("Checkout", "Reservas", new { id = idReserva })
                    : RedirectToAction("Detalle", new { id = idReserva });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo registrar el abono: " + ex.Message;
                return RedirectToAction("Detalle", new { id = idReserva });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarAbonoGrupo(
            int idReservaGrupo,
            decimal monto,
            int idFormaPago,
            string? referencia,
            string? observaciones,
            int? idReservaObjetivo = null,
            int? idReservaRetorno = null,
            bool solicitarFacturaAlLiquidar = false,
            bool volverCheckout = false)
        {
            IActionResult? acceso = ValidarCobro();

            if (acceso != null)
            {
                return acceso;
            }

            if (monto <= 0)
            {
                TempData["Mensaje"] = "El abono debe ser mayor que Q0.00.";
                return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                int idCliente;

                using (var comando = new MySqlCommand(@"
                    SELECT id_clipro
                    FROM reserva_grupo
                    WHERE id_reserva_grupo = @id_reserva_grupo
                    LIMIT 1
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                    object? resultado = comando.ExecuteScalar();

                    if (resultado == null)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reserva agrupada.";
                        return RedirectToAction("Index");
                    }

                    idCliente = Convert.ToInt32(resultado);
                }

                var reservas = new List<(int IdReserva, decimal SaldoPendiente)>();

                using (var comando = new MySqlCommand(@"
                    SELECT id_reserva, saldo_pendiente, estado
                    FROM reserva
                    WHERE id_reserva_grupo = @id_reserva_grupo
                    ORDER BY fecha_entrada, id_reserva
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                    using var lector = comando.ExecuteReader();

                    string[] estadosPermitidos = { "pendiente", "en_curso", "en_checkout", "finalizada" };

                    while (lector.Read())
                    {
                        string estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";
                        decimal saldo = Convert.ToDecimal(lector["saldo_pendiente"]);

                        if (estadosPermitidos.Contains(estado) && saldo > 0)
                        {
                            reservas.Add((Convert.ToInt32(lector["id_reserva"]), saldo));
                        }
                    }
                }

                decimal saldoTotal = reservas.Sum(reserva => reserva.SaldoPendiente);

                if (idReservaObjetivo.HasValue)
                {
                    var reservaObjetivo = reservas.FirstOrDefault(
                        reserva => reserva.IdReserva == idReservaObjetivo.Value);

                    if (reservaObjetivo.IdReserva == 0)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "La estadía seleccionada no pertenece al grupo o ya no tiene saldo pendiente.";
                        return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                    }

                    reservas = new List<(int IdReserva, decimal SaldoPendiente)> { reservaObjetivo };
                    saldoTotal = reservaObjetivo.SaldoPendiente;
                }

                if (saldoTotal <= 0)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La reserva agrupada no tiene saldo pendiente.";
                    return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                }

                if (monto > saldoTotal)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = idReservaObjetivo.HasValue
                        ? "El abono no puede ser mayor que el saldo pendiente de la estadía seleccionada."
                        : "El abono no puede ser mayor que el saldo pendiente del grupo.";
                    return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                }

                if (solicitarFacturaAlLiquidar && monto != saldoTotal)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Para solicitar la factura desde este pago debe liquidar el saldo completo seleccionado.";
                    return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                }

                string? nombreFormaPago = ObtenerNombreFormaPago(conexion, transaccion, idFormaPago);

                if (string.IsNullOrWhiteSpace(nombreFormaPago))
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La forma de pago seleccionada no es válida.";
                    return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                }

                decimal recargoTarjeta = EsFormaPagoTarjeta(nombreFormaPago)
                    ? ObtenerRecargoTarjeta(conexion, transaccion)
                    : 0m;

                int idTipoAbono;

                using (var comando = new MySqlCommand(@"
                    SELECT id_tipomov
                    FROM tipo_movimiento
                    WHERE LOWER(nombre_tipomov) = 'abono'
                    LIMIT 1;", conexion, transaccion))
                {
                    object? resultado = comando.ExecuteScalar();

                    if (resultado == null)
                    {
                        throw new InvalidOperationException(
                            "No existe el tipo de movimiento 'abono'. Aplique primero el cambio SQL indicado.");
                    }

                    idTipoAbono = Convert.ToInt32(resultado);
                }

                string observacionCompleta = string.Join(
                    " | ",
                    new[] { referencia?.Trim(), observaciones?.Trim() }
                        .Where(valor => !string.IsNullOrWhiteSpace(valor)));

                if (observacionCompleta.Length > 255)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La referencia y las observaciones no pueden superar 255 caracteres en total.";
                    return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                }

                int idMovimiento;

                using (var comando = new MySqlCommand(@"
                    INSERT INTO movimiento
                    (
                        id_usuario,
                        id_clipro,
                        id_tipomov,
                        id_formapago,
                        id_reserva,
                        id_reserva_grupo,
                        recargo_tarjeta,
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
                        NULL,
                        @id_reserva_grupo,
                        @recargo_tarjeta,
                        NOW(),
                        'activo',
                        @observaciones
                    );", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_usuario", ObtenerIdUsuarioSesion());
                    comando.Parameters.AddWithValue("@id_clipro", idCliente);
                    comando.Parameters.AddWithValue("@id_tipomov", idTipoAbono);
                    comando.Parameters.AddWithValue("@id_formapago", idFormaPago);
                    comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                    comando.Parameters.AddWithValue("@recargo_tarjeta", recargoTarjeta);
                    comando.Parameters.AddWithValue(
                        "@observaciones",
                        string.IsNullOrWhiteSpace(observacionCompleta)
                            ? DBNull.Value
                            : observacionCompleta);
                    comando.ExecuteNonQuery();
                    idMovimiento = Convert.ToInt32(comando.LastInsertedId);
                }

                using (var comando = new MySqlCommand(@"
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
                    );", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);
                    comando.Parameters.AddWithValue("@monto", monto);
                    comando.Parameters.AddWithValue(
                        "@descripcion",
                        idReservaObjetivo.HasValue
                            ? $"Abono a estadía #{idReservaObjetivo.Value} del grupo #{idReservaGrupo}"
                            : $"Abono a reserva agrupada #{idReservaGrupo}");
                    comando.ExecuteNonQuery();
                }

                decimal montoRestante = monto;

                foreach (var reserva in reservas)
                {
                    if (montoRestante <= 0)
                    {
                        break;
                    }

                    decimal montoAplicado = Math.Min(reserva.SaldoPendiente, montoRestante);

                    using (var comando = new MySqlCommand(@"
                        UPDATE reserva
                        SET saldo_pendiente = saldo_pendiente - @monto
                        WHERE id_reserva = @id_reserva;", conexion, transaccion))
                    {
                        comando.Parameters.AddWithValue("@monto", montoAplicado);
                        comando.Parameters.AddWithValue("@id_reserva", reserva.IdReserva);
                        comando.ExecuteNonQuery();
                    }

                    using (var comando = new MySqlCommand(@"
                        INSERT INTO movimiento_reserva_aplicacion
                            (id_movimiento, id_reserva, monto)
                        VALUES
                            (@id_movimiento, @id_reserva, @monto);", conexion, transaccion))
                    {
                        comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);
                        comando.Parameters.AddWithValue("@id_reserva", reserva.IdReserva);
                        comando.Parameters.AddWithValue("@monto", montoAplicado);
                        comando.ExecuteNonQuery();
                    }

                    montoRestante -= montoAplicado;
                }

                if (solicitarFacturaAlLiquidar)
                {
                    int idReservaSolicitud = idReservaObjetivo
                        ?? (idReservaRetorno.HasValue && reservas.Any(r => r.IdReserva == idReservaRetorno.Value)
                            ? idReservaRetorno.Value
                            : reservas[0].IdReserva);

                    _facturacionService.RegistrarDecision(
                        conexion,
                        transaccion,
                        idReservaSolicitud,
                        true,
                        ObtenerIdUsuarioSesion(),
                        "pago_final",
                        idReservaObjetivo.HasValue
                            ? "Factura solicitada al liquidar una estadía de la reserva agrupada."
                            : "Factura solicitada al liquidar la cuenta agrupada después de la estadía.");
                }

                transaccion.Commit();
                string resumenRecargo = recargoTarjeta > 0
                    ? $" Se aplicó un recargo de Q{recargoTarjeta:N2}; total cobrado Q{monto + recargoTarjeta:N2}."
                    : "";
                TempData["Exito"] = solicitarFacturaAlLiquidar
                    ? "Pago registrado y solicitud enviada a Facturas pendientes."
                    : idReservaObjetivo.HasValue
                        ? "Abono registrado correctamente para la estadía seleccionada."
                        : "Abono registrado correctamente en la cuenta agrupada.";
                TempData["Exito"] = (TempData["Exito"]?.ToString() ?? "") + resumenRecargo;

                return volverCheckout && idReservaRetorno.HasValue
                    ? RedirectToAction("Checkout", "Reservas", new { id = idReservaRetorno.Value })
                    : RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo registrar el abono agrupado: " + ex.Message;
                return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AnularAbono(
            int idMovimiento,
            int idReserva,
            bool volverCheckout = false)
        {
            IActionResult? acceso = ValidarCobro();

            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                string consultaAbono = @"
                    SELECT
                        m.id_movimiento,
                        (
                            SELECT COALESCE(SUM(d.subtotal), 0)
                            FROM detalle d
                            WHERE d.id_movimiento = m.id_movimiento
                        ) AS monto
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm
                        ON m.id_tipomov = tm.id_tipomov
                    WHERE m.id_movimiento = @id_movimiento
                      AND m.id_reserva = @id_reserva
                      AND m.estado = 'activo'
                      AND LOWER(tm.nombre_tipomov) = 'abono'
                    LIMIT 1
                    FOR UPDATE;";

                decimal monto;

                using (var comando = new MySqlCommand(consultaAbono, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);

                    using var lector = comando.ExecuteReader();

                    if (!lector.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "El abono no existe o ya fue anulado.";
                        return RedirectToAction("Detalle", new { id = idReserva });
                    }

                    monto = Convert.ToDecimal(lector["monto"]);
                }

                using (var bloquearReserva = new MySqlCommand(@"
                    SELECT id_reserva
                    FROM reserva
                    WHERE id_reserva = @id_reserva
                    FOR UPDATE;", conexion, transaccion))
                {
                    bloquearReserva.Parameters.AddWithValue("@id_reserva", idReserva);
                    bloquearReserva.ExecuteScalar();
                }

                using (var comando = new MySqlCommand(@"
                    UPDATE movimiento
                    SET estado = 'anulado'
                    WHERE id_movimiento = @id_movimiento
                      AND estado = 'activo';", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);

                    if (comando.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException("El abono ya no se encuentra activo.");
                    }
                }

                using (var comando = new MySqlCommand(@"
                    UPDATE reserva
                    SET saldo_pendiente = saldo_pendiente + @monto
                    WHERE id_reserva = @id_reserva;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@monto", monto);
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                TempData["Exito"] = "El abono fue anulado y el saldo se actualizó.";

                return volverCheckout
                    ? RedirectToAction("Checkout", "Reservas", new { id = idReserva })
                    : RedirectToAction("Detalle", new { id = idReserva });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo anular el abono: " + ex.Message;
                return RedirectToAction("Detalle", new { id = idReserva });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AnularAbonoGrupo(
            int idMovimiento,
            int idReservaGrupo,
            int? idReservaRetorno = null,
            bool volverCheckout = false)
        {
            IActionResult? acceso = ValidarCobro();

            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                using (var comando = new MySqlCommand(@"
                    SELECT id_reserva_grupo
                    FROM reserva_grupo
                    WHERE id_reserva_grupo = @id_reserva_grupo
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);

                    if (comando.ExecuteScalar() == null)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reserva agrupada.";
                        return RedirectToAction("Index");
                    }
                }

                using (var comando = new MySqlCommand(@"
                    SELECT id_reserva
                    FROM reserva
                    WHERE id_reserva_grupo = @id_reserva_grupo
                    ORDER BY fecha_entrada, id_reserva
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);
                    using var lector = comando.ExecuteReader();

                    while (lector.Read())
                    {
                        // Consumir todas las filas mantiene estable la distribución del abono.
                    }
                }

                using (var comando = new MySqlCommand(@"
                    SELECT m.id_movimiento
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                    WHERE m.id_movimiento = @id_movimiento
                      AND m.id_reserva_grupo = @id_reserva_grupo
                      AND m.estado = 'activo'
                      AND LOWER(tm.nombre_tipomov) = 'abono'
                    LIMIT 1
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);
                    comando.Parameters.AddWithValue("@id_reserva_grupo", idReservaGrupo);

                    if (comando.ExecuteScalar() == null)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "El abono no existe o ya fue anulado.";
                        return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
                    }
                }

                var aplicaciones = new List<(int IdReserva, decimal Monto)>();

                using (var comando = new MySqlCommand(@"
                    SELECT a.id_reserva, a.monto
                    FROM movimiento_reserva_aplicacion a
                    INNER JOIN reserva r ON a.id_reserva = r.id_reserva
                    WHERE a.id_movimiento = @id_movimiento
                    ORDER BY r.fecha_entrada, r.id_reserva
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);
                    using var lector = comando.ExecuteReader();

                    while (lector.Read())
                    {
                        aplicaciones.Add((
                            Convert.ToInt32(lector["id_reserva"]),
                            Convert.ToDecimal(lector["monto"])));
                    }
                }

                if (aplicaciones.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No se encontró la distribución del abono entre las estadías agrupadas.");
                }

                using (var comando = new MySqlCommand(@"
                    UPDATE movimiento
                    SET estado = 'anulado'
                    WHERE id_movimiento = @id_movimiento
                      AND estado = 'activo';", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_movimiento", idMovimiento);

                    if (comando.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException("El abono ya no se encuentra activo.");
                    }
                }

                foreach (var aplicacion in aplicaciones)
                {
                    using var comando = new MySqlCommand(@"
                        UPDATE reserva
                        SET saldo_pendiente = saldo_pendiente + @monto
                        WHERE id_reserva = @id_reserva;", conexion, transaccion);
                    comando.Parameters.AddWithValue("@monto", aplicacion.Monto);
                    comando.Parameters.AddWithValue("@id_reserva", aplicacion.IdReserva);
                    comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                TempData["Exito"] = "El abono agrupado fue anulado y el saldo se actualizó.";

                return volverCheckout && idReservaRetorno.HasValue
                    ? RedirectToAction("Checkout", "Reservas", new { id = idReservaRetorno.Value })
                    : RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudo anular el abono agrupado: " + ex.Message;
                return RedirectToAction("DetalleGrupo", new { id = idReservaGrupo, idReservaRetorno });
            }
        }
    }
}
