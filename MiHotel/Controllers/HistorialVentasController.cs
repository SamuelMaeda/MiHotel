using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class HistorialVentasController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public HistorialVentasController(ConexionBD conexionBD)
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
    string ordenarPor = "fecha_hora",
    string direccion = "desc",
    int pagina = 1)
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();

            conexion.Open();

            int registrosPorPagina = 10;

            var columnasPermitidas = new List<string>
    {
        "id_movimiento",
        "fecha_hora",
        "cliente",
        "habitacion",
        "usuario",
        "total"
    };

            if (!columnasPermitidas.Contains(ordenarPor))
            {
                ordenarPor = "fecha_hora";
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
            OR u.nombre_usuario LIKE @busqueda
            OR h.nombre_proser LIKE @busqueda
            OR m.id_movimiento LIKE @busqueda
        )";
            }

            string queryTotal = $@"
    SELECT COUNT(DISTINCT m.id_movimiento)

    FROM movimiento m

    INNER JOIN tipo_movimiento tm
        ON m.id_tipomov = tm.id_tipomov

    INNER JOIN clipro c
        ON m.id_clipro = c.id_clipro

    INNER JOIN usuario u
        ON m.id_usuario = u.id_usuario

    LEFT JOIN reserva r
        ON m.id_reserva = r.id_reserva

    LEFT JOIN proser h
        ON r.id_habitacion = h.id_proser

    WHERE LOWER(tm.nombre_tipomov) = 'venta'
    {whereBusqueda}
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
        m.fecha_hora,
        c.nombre AS cliente,
        u.nombre_usuario AS usuario,
        m.estado,

        h.nombre_proser AS habitacion,

        SUM(d.subtotal) AS total

    FROM movimiento m

    INNER JOIN tipo_movimiento tm
        ON m.id_tipomov = tm.id_tipomov

    INNER JOIN clipro c
        ON m.id_clipro = c.id_clipro

    INNER JOIN usuario u
        ON m.id_usuario = u.id_usuario

    INNER JOIN detalle d
        ON m.id_movimiento = d.id_movimiento

    LEFT JOIN reserva r
        ON m.id_reserva = r.id_reserva

    LEFT JOIN proser h
        ON r.id_habitacion = h.id_proser

    WHERE LOWER(tm.nombre_tipomov) = 'venta'
    {whereBusqueda}

    GROUP BY
        m.id_movimiento,
        m.fecha_hora,
        c.nombre,
        u.nombre_usuario,
        m.estado,
        h.nombre_proser

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