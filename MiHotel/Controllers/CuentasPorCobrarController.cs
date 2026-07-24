using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class CuentasPorCobrarController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public CuentasPorCobrarController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarSesion()
        {
            if (HttpContext.Session.GetString("IdUsuario") == null)
                return RedirectToAction("Login", "Acceso");

            return null;
        }

        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "fecha_entrada",
            string direccion = "desc",
            int pagina = 1)
        {
            var acceso = ValidarSesion();

            if (acceso != null)
                return acceso;

            using var conexion = _conexionBD.ObtenerConexion();

            conexion.Open();

            int registrosPorPagina = 10;

            var columnasPermitidas = new List<string>
            {
                "cliente",
                "habitacion",
                "fecha_entrada",
                "fecha_salida",
                "saldo"
            };

            if (!columnasPermitidas.Contains(ordenarPor))
            {
                ordenarPor = "fecha_entrada";
            }

            direccion = direccion.ToLower() == "asc"
                ? "asc"
                : "desc";

            string whereBusqueda = "";

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                whereBusqueda = @"
                    AND (
                        c.nombre LIKE @busqueda
                        OR h.nombre_proser LIKE @busqueda
                        OR r.id_reserva LIKE @busqueda
                    )";
            }
            string queryTotal = $@"

SELECT COUNT(*)

FROM movimiento m

INNER JOIN tipo_movimiento tm
    ON m.id_tipomov = tm.id_tipomov

INNER JOIN clipro c
    ON m.id_clipro = c.id_clipro

LEFT JOIN reserva r
    ON m.id_reserva = r.id_reserva

LEFT JOIN proser h
    ON r.id_habitacion = h.id_proser

INNER JOIN detalle d
    ON m.id_movimiento = d.id_movimiento

WHERE LOWER(tm.nombre_tipomov) = 'cuenta_por_cobrar'

AND d.subtotal > 0

{whereBusqueda};
";

            var cmdTotal = new MySqlCommand(queryTotal, conexion);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                cmdTotal.Parameters.AddWithValue("@busqueda", $"%{busqueda}%");
            }

            int totalRegistros = Convert.ToInt32(cmdTotal.ExecuteScalar());

            int totalPaginas = (int)Math.Ceiling(
                (double)totalRegistros / registrosPorPagina);

            int offset = (pagina - 1) * registrosPorPagina;

            string query = $@"

SELECT

    m.id_movimiento,

    m.id_reserva,

    c.id_clipro,

    c.nombre AS cliente,

    h.nombre_proser AS habitacion,

    r.fecha_entrada,

    r.fecha_salida,

    m.fecha_hora,

    SUM(d.subtotal) AS saldo

FROM movimiento m

INNER JOIN tipo_movimiento tm
    ON m.id_tipomov = tm.id_tipomov

INNER JOIN clipro c
    ON m.id_clipro = c.id_clipro

LEFT JOIN reserva r
    ON m.id_reserva = r.id_reserva

LEFT JOIN proser h
    ON r.id_habitacion = h.id_proser

INNER JOIN detalle d
    ON m.id_movimiento = d.id_movimiento

WHERE LOWER(tm.nombre_tipomov) = 'cuenta_por_cobrar'

AND d.subtotal > 0

{whereBusqueda}

GROUP BY

    m.id_movimiento,
    m.id_reserva,
    c.id_clipro,
    c.nombre,
    h.nombre_proser,
    r.fecha_entrada,
    r.fecha_salida,
    m.fecha_hora

ORDER BY {ordenarPor} {direccion}

LIMIT @limit OFFSET @offset;
";
            var cmd = new MySqlCommand(query, conexion);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                cmd.Parameters.AddWithValue("@busqueda", $"%{busqueda}%");
            }

            cmd.Parameters.AddWithValue("@limit", registrosPorPagina);
            cmd.Parameters.AddWithValue("@offset", offset);

            var dt = new DataTable();

            new MySqlDataAdapter(cmd).Fill(dt);

            ViewBag.Busqueda = busqueda;
            ViewBag.OrdenarPor = ordenarPor;
            ViewBag.Direccion = direccion;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;

            return View(dt);


        }
    }
}
