// Controlador de categorías - CRUD completo con manejo de estado y protección de categorías del sistema

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class CategoriasController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public CategoriasController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ================= INDEX =================
        public IActionResult Index(string busqueda = "", string vista = "activos")
        {
            DataTable tabla = new DataTable();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string estado = vista == "inactivos" ? "inactivo" : "activo";

            string sql = @"SELECT id_categoria, nombre_categoria, estado, es_sistema
                           FROM categoria
                           WHERE estado = @estado
                           AND nombre_categoria LIKE @busqueda
                           ORDER BY nombre_categoria ASC";

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
        public IActionResult Crear(string nombre_categoria)
        {
            if (string.IsNullOrWhiteSpace(nombre_categoria))
            {
                ViewBag.Mensaje = "El nombre es obligatorio.";
                return View();
            }

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            // Verificar duplicado
            string verificar = @"SELECT COUNT(*) 
                                 FROM categoria 
                                 WHERE LOWER(nombre_categoria) = LOWER(@nombre)";

            using var cmdVerificar = new MySqlCommand(verificar, conexion);
            cmdVerificar.Parameters.AddWithValue("@nombre", nombre_categoria.Trim());

            int existe = Convert.ToInt32(cmdVerificar.ExecuteScalar());

            if (existe > 0)
            {
                ViewBag.Mensaje = "La categoría ya existe.";
                return View();
            }

            string insertar = @"INSERT INTO categoria (nombre_categoria, estado, es_sistema)
                                VALUES (@nombre, 'activo', 0)";

            using var cmd = new MySqlCommand(insertar, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre_categoria.Trim());

            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Categoría creada correctamente.";

            return RedirectToAction("Index");
        }

        // ================= EDITAR =================
        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = @"SELECT id_categoria, nombre_categoria, es_sistema
                           FROM categoria
                           WHERE id_categoria = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@id", id);

            using var dr = cmd.ExecuteReader();

            if (!dr.Read())
                return RedirectToAction("Index");

            // PROTECCIÓN
            if (Convert.ToInt32(dr["es_sistema"]) == 1)
            {
                TempData["Mensaje"] = "Esta categoría es del sistema y no puede ser modificada.";
                return RedirectToAction("Index");
            }

            ViewBag.Id = id;
            ViewBag.Nombre = dr["nombre_categoria"].ToString();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, string nombre_categoria)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            // Verificar si es sistema
            string verificarSistema = @"SELECT es_sistema FROM categoria WHERE id_categoria = @id";

            using var cmdSistema = new MySqlCommand(verificarSistema, conexion);
            cmdSistema.Parameters.AddWithValue("@id", id);

            int esSistema = Convert.ToInt32(cmdSistema.ExecuteScalar());

            if (esSistema == 1)
            {
                TempData["Mensaje"] = "Esta categoría es del sistema y no puede ser modificada.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(nombre_categoria))
            {
                ViewBag.Mensaje = "El nombre es obligatorio.";
                ViewBag.Id = id;
                return View();
            }

            // Verificar duplicado (excluyendo el mismo registro)
            string verificarDuplicado = @"SELECT COUNT(*) 
                                         FROM categoria 
                                         WHERE LOWER(nombre_categoria) = LOWER(@nombre)
                                         AND id_categoria != @id";

            using var cmdVerificar = new MySqlCommand(verificarDuplicado, conexion);
            cmdVerificar.Parameters.AddWithValue("@nombre", nombre_categoria.Trim());
            cmdVerificar.Parameters.AddWithValue("@id", id);

            int existe = Convert.ToInt32(cmdVerificar.ExecuteScalar());

            if (existe > 0)
            {
                ViewBag.Mensaje = "Ya existe otra categoría con ese nombre.";
                ViewBag.Id = id;
                return View();
            }

            string sql = @"UPDATE categoria
                           SET nombre_categoria = @nombre
                           WHERE id_categoria = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre_categoria.Trim());
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Categoría actualizada correctamente.";

            return RedirectToAction("Index");
        }

        // ================= CAMBIAR ESTADO =================
        [HttpPost]
        public IActionResult CambiarEstado(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            // Verificar si es sistema
            string verificarSistema = @"SELECT es_sistema FROM categoria WHERE id_categoria = @id";

            using var cmdSistema = new MySqlCommand(verificarSistema, conexion);
            cmdSistema.Parameters.AddWithValue("@id", id);

            int esSistema = Convert.ToInt32(cmdSistema.ExecuteScalar());

            if (esSistema == 1)
            {
                TempData["Mensaje"] = "Esta categoría es del sistema y no puede ser desactivada.";
                return RedirectToAction("Index");
            }

            // Obtener estado actual
            string estadoActual = new MySqlCommand(
                "SELECT estado FROM categoria WHERE id_categoria = @id", conexion)
            {
                Parameters = { new MySqlParameter("@id", id) }
            }.ExecuteScalar()?.ToString();

            string nuevoEstado = estadoActual == "activo" ? "inactivo" : "activo";

            string sql = @"UPDATE categoria 
                           SET estado = @estado 
                           WHERE id_categoria = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@estado", nuevoEstado);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Estado actualizado correctamente.";

            return RedirectToAction("Index");
        }
    }
}