using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MiHotel.Data;
using System.Data;

namespace MiHotel.Controllers
{
    public class InventarioController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public InventarioController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarSesion()
        {
            if (HttpContext.Session.GetString("IdUsuario") == null)
                return RedirectToAction("Login", "Acceso");

            return null;
        }

        public IActionResult Index(string busqueda = "")
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = @"
            SELECT 
                p.id_proser,
                p.nombre_proser,
                p.stock,
                p.precio,
                s.nombre_subcategoria
            FROM proser p
            LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
            WHERE p.id_tipoproser = (
                SELECT id_tipoproser 
                FROM tipo_proser 
                WHERE LOWER(nombre) = 'producto'
                LIMIT 1
            )
            AND p.nombre_proser LIKE @busqueda
            ORDER BY p.nombre_proser ASC";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@busqueda", "%" + busqueda + "%");

            var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);

            ViewBag.Productos = dt;

            return View();
        }
    }
}