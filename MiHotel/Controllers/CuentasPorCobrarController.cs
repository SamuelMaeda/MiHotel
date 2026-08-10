// ================================================
// CONTROLADOR DE CUENTAS POR COBRAR
// Administra el listado de cuentas pendientes
// agrupadas por estadía y las cuentas independientes.
// ================================================

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class CuentasPorCobrarController : Controller
    {
        private readonly ConexionBD _conexionBD;

        private const int RegistrosPorPagina = 20;

        public CuentasPorCobrarController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
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
                "habitacion" => "cuenta.habitacion",
                "fecha_entrada" => "cuenta.fecha_entrada",
                "fecha_salida" => "cuenta.fecha_salida",
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
            string vista = "estadias",
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

            string vistaNormalizada =
                vista?.Trim().ToLower() == "estadias"
                    ? "estadias"
                    : "estadias";

            if (pagina < 1)
            {
                pagina = 1;
            }

            string columnaOrden = ObtenerColumnaOrden(ordenarPor);

            ViewBag.Busqueda = busqueda;
            ViewBag.OrdenarPor = ordenarPor;
            ViewBag.Direccion = direccionNormalizada;
            ViewBag.Vista = vistaNormalizada;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = 1;
            ViewBag.TotalRegistros = 0;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();

                conexion.Open();

                /*
                 * La primera parte agrupa todos los movimientos CxC
                 * relacionados con una reservación mediante id_reserva.
                 *
                 * La segunda parte conserva como cuentas independientes
                 * los movimientos que no poseen una reservación.
                 */

                string consultaBase = @"
                    SELECT
                        'estadia' AS tipo_cuenta,
                        r.id_reserva,
                        MIN(m.id_movimiento) AS id_movimiento_referencia,
                        r.id_clipro,
                        c.nombre AS cliente,
                        h.nombre_proser AS habitacion,
                        r.fecha_entrada,
                        r.fecha_salida,
                        MIN(m.fecha_hora) AS fecha_cuenta,
                        SUM(d.subtotal) AS saldo,
                        COUNT(DISTINCT m.id_movimiento)
                            AS cantidad_movimientos
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm
                        ON m.id_tipomov = tm.id_tipomov
                    INNER JOIN reserva r
                        ON m.id_reserva = r.id_reserva
                    INNER JOIN clipro c
                        ON r.id_clipro = c.id_clipro
                    LEFT JOIN proser h
                        ON r.id_habitacion = h.id_proser
                    INNER JOIN detalle d
                        ON m.id_movimiento = d.id_movimiento
                    WHERE LOWER(tm.nombre_tipomov) =
                              'cuenta_por_cobrar'
                      AND m.estado = 'activo'
                      AND m.id_reserva IS NOT NULL
                      AND d.subtotal > 0
                    GROUP BY
                        r.id_reserva,
                        r.id_clipro,
                        c.nombre,
                        h.nombre_proser,
                        r.fecha_entrada,
                        r.fecha_salida

                    UNION ALL

                    SELECT
                        'independiente' AS tipo_cuenta,
                        NULL AS id_reserva,
                        m.id_movimiento
                            AS id_movimiento_referencia,
                        m.id_clipro,
                        c.nombre AS cliente,
                        NULL AS habitacion,
                        NULL AS fecha_entrada,
                        NULL AS fecha_salida,
                        m.fecha_hora AS fecha_cuenta,
                        SUM(d.subtotal) AS saldo,
                        1 AS cantidad_movimientos
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm
                        ON m.id_tipomov = tm.id_tipomov
                    INNER JOIN clipro c
                        ON m.id_clipro = c.id_clipro
                    INNER JOIN detalle d
                        ON m.id_movimiento = d.id_movimiento
                    WHERE LOWER(tm.nombre_tipomov) =
                              'cuenta_por_cobrar'
                      AND m.estado = 'activo'
                      AND m.id_reserva IS NULL
                      AND d.subtotal > 0
                    GROUP BY
                        m.id_movimiento,
                        m.id_clipro,
                        c.nombre,
                        m.fecha_hora";

                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    condicionBusqueda = @"
                        AND (
                            cuenta.cliente LIKE @busqueda
                            OR cuenta.habitacion LIKE @busqueda
                            OR CAST(
                                cuenta.id_reserva AS CHAR
                            ) LIKE @busqueda
                            OR CAST(
                                cuenta.id_movimiento_referencia AS CHAR
                            ) LIKE @busqueda
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
                        cuenta.id_movimiento_referencia,
                        cuenta.id_clipro,
                        cuenta.cliente,
                        cuenta.habitacion,
                        cuenta.fecha_entrada,
                        cuenta.fecha_salida,
                        cuenta.fecha_cuenta,
                        cuenta.saldo,
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
    }
}