using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class MarcasController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public MarcasController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ================= INDEX =================
        public IActionResult Index(
            string vista = "activos",
            string busqueda = ""
        )
        {
            DataTable tabla = new DataTable();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string estado = vista == "inactivos" ? "inactivo" : "activo";

            string sql = @"
            SELECT id_marca, nombre_marca, estado
            FROM marca
            WHERE estado = @estado
            AND nombre_marca LIKE @busqueda
            ORDER BY nombre_marca ASC";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@busqueda", "%" + busqueda + "%");

            new MySqlDataAdapter(cmd).Fill(tabla);

            ViewBag.Busqueda = busqueda;
            ViewBag.Vista = vista;

            return View(tabla);
        }

        // ================= CREAR =================
        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                ViewBag.Mensaje = "El nombre es obligatorio.";
                return View();
            }

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string verificar = @"SELECT COUNT(*) FROM marca WHERE LOWER(nombre_marca) = LOWER(@nombre)";

            using var cmdVerificar = new MySqlCommand(verificar, conexion);
            cmdVerificar.Parameters.AddWithValue("@nombre", nombre.Trim());

            int existe = Convert.ToInt32(cmdVerificar.ExecuteScalar());

            if (existe > 0)
            {
                ViewBag.Mensaje = "La marca ya existe.";
                return View();
            }

            string insertar = @"INSERT INTO marca (nombre_marca, estado)
                    VALUES (@nombre, 'activo')";

            using var cmd = new MySqlCommand(insertar, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre.Trim());

            cmd.ExecuteNonQuery();

            return RedirectToAction("Index");
        }

        // ================= EDITAR =================
        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = @"SELECT id_marca, nombre_marca 
               FROM marca 
               WHERE id_marca = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                ViewBag.Id = reader["id_marca"];
                ViewBag.Nombre = reader["nombre_marca"];
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, string nombre)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = @"UPDATE marca 
                           SET nombre_marca = @nombre
                           WHERE id_marca = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();

            return RedirectToAction("Index");
        }

        // ================= CAMBIAR ESTADO =================
        [HttpPost]
        public IActionResult CambiarEstado(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string estadoActual = new MySqlCommand(
                "SELECT estado FROM marca WHERE id_marca = @id", conexion)
            {
                Parameters = { new MySqlParameter("@id", id) }
            }.ExecuteScalar()?.ToString();

            string nuevoEstado = estadoActual == "activo" ? "inactivo" : "activo";

            string sql = @"UPDATE marca SET estado = @estado WHERE id_marca = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@estado", nuevoEstado);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();

            return RedirectToAction("Index");
        }
    }
}