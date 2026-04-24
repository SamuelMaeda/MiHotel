// Controlador de subcategorías - CRUD completo con filtros por tipo y estado

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class SubcategoriasController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public SubcategoriasController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ================= INDEX =================
        public IActionResult Index(
            string tipo = "habitaciones",
            string vista = "activos",
            string busqueda = "",
            string ordenarPor = "subcategoria",
            string direccion = "asc"
        )
        {
            DataTable tabla = new DataTable();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string estado = vista == "inactivos" ? "inactivo" : "activo";

            // ================= FILTRO POR TIPO =================
            string filtroTipo = "";

            if (tipo == "habitaciones")
            {
                filtroTipo = "AND c.nombre_categoria = 'Habitaciones'";
            }
            else if (tipo == "productos")
            {
                filtroTipo = "AND c.nombre_categoria <> 'Habitaciones'";
            }
            else if (tipo == "servicios")
            {
                filtroTipo = "AND c.nombre_categoria = 'Servicios'";
            }

            // ================= ORDENAMIENTO =================
            string columnaOrden = ordenarPor switch
            {
                "subcategoria" => "s.nombre_subcategoria",
                "categoria" => "c.nombre_categoria",
                _ => "s.nombre_subcategoria"
            };

            string direccionOrden = direccion == "desc" ? "DESC" : "ASC";

            string sql = $@"
        SELECT s.id_subcategoria,
               s.nombre_subcategoria,
               s.estado,
               c.nombre_categoria
        FROM subcategoria s
        INNER JOIN categoria c ON s.id_categoria = c.id_categoria
        WHERE s.estado = @estado
        AND s.nombre_subcategoria LIKE @busqueda
        {filtroTipo}
        ORDER BY {columnaOrden} {direccionOrden}
    ";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@busqueda", "%" + busqueda + "%");

            new MySqlDataAdapter(cmd).Fill(tabla);

            ViewBag.Busqueda = busqueda;
            ViewBag.Vista = vista;
            ViewBag.Tipo = tipo;
            ViewBag.OrdenarPor = ordenarPor;
            ViewBag.Direccion = direccion;

            return View(tabla);
        }

        // ================= CREAR =================
        [HttpGet]
        public IActionResult Crear()
        {
            CargarCategorias();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(string nombre_subcategoria, int id_categoria)
        {
            if (string.IsNullOrWhiteSpace(nombre_subcategoria) || id_categoria == 0)
            {
                ViewBag.Mensaje = "Todos los campos son obligatorios.";
                CargarCategorias();
                return View();
            }

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string verificar = @"SELECT COUNT(*) 
                                 FROM subcategoria 
                                 WHERE LOWER(nombre_subcategoria) = LOWER(@nombre)";

            using var cmdVerificar = new MySqlCommand(verificar, conexion);
            cmdVerificar.Parameters.AddWithValue("@nombre", nombre_subcategoria.Trim());

            int existe = Convert.ToInt32(cmdVerificar.ExecuteScalar());

            if (existe > 0)
            {
                ViewBag.Mensaje = "La subcategoría ya existe.";
                CargarCategorias();
                return View();
            }

            string insertar = @"INSERT INTO subcategoria (nombre_subcategoria, id_categoria, estado)
                                VALUES (@nombre, @categoria, 'activo')";

            using var cmd = new MySqlCommand(insertar, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre_subcategoria.Trim());
            cmd.Parameters.AddWithValue("@categoria", id_categoria);

            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Subcategoría creada correctamente.";

            return RedirectToAction("Index");
        }

        // ================= EDITAR =================
        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = @"SELECT * FROM subcategoria WHERE id_subcategoria = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@id", id);

            using var dr = cmd.ExecuteReader();

            if (!dr.Read())
                return RedirectToAction("Index");

            ViewBag.Id = id;
            ViewBag.Nombre = dr["nombre_subcategoria"].ToString();
            ViewBag.IdCategoria = Convert.ToInt32(dr["id_categoria"]);

            CargarCategorias();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, string nombre_subcategoria, int id_categoria)
        {
            if (string.IsNullOrWhiteSpace(nombre_subcategoria) || id_categoria == 0)
            {
                ViewBag.Mensaje = "Todos los campos son obligatorios.";
                ViewBag.Id = id;
                CargarCategorias();
                return View();
            }

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string verificar = @"SELECT COUNT(*) 
                                 FROM subcategoria 
                                 WHERE LOWER(nombre_subcategoria) = LOWER(@nombre)
                                 AND id_subcategoria != @id";

            using var cmdVerificar = new MySqlCommand(verificar, conexion);
            cmdVerificar.Parameters.AddWithValue("@nombre", nombre_subcategoria.Trim());
            cmdVerificar.Parameters.AddWithValue("@id", id);

            int existe = Convert.ToInt32(cmdVerificar.ExecuteScalar());

            if (existe > 0)
            {
                ViewBag.Mensaje = "Ya existe otra subcategoría con ese nombre.";
                ViewBag.Id = id;
                CargarCategorias();
                return View();
            }

            string sql = @"UPDATE subcategoria
                           SET nombre_subcategoria = @nombre,
                               id_categoria = @categoria
                           WHERE id_subcategoria = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre_subcategoria.Trim());
            cmd.Parameters.AddWithValue("@categoria", id_categoria);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Subcategoría actualizada correctamente.";

            return RedirectToAction("Index");
        }

        // ================= CAMBIAR ESTADO =================
        [HttpPost]
        public IActionResult CambiarEstado(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string estadoActual = new MySqlCommand(
                "SELECT estado FROM subcategoria WHERE id_subcategoria = @id", conexion)
            {
                Parameters = { new MySqlParameter("@id", id) }
            }.ExecuteScalar()?.ToString();

            string nuevoEstado = estadoActual == "activo" ? "inactivo" : "activo";

            string sql = @"UPDATE subcategoria 
                           SET estado = @estado 
                           WHERE id_subcategoria = @id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@estado", nuevoEstado);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();

            return RedirectToAction("Index");
        }

        // ================= MÉTODO AUXILIAR =================
        private void CargarCategorias()
        {
            DataTable tabla = new DataTable();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = @"SELECT id_categoria, nombre_categoria
                           FROM categoria
                           WHERE estado = 'activo'
                           ORDER BY nombre_categoria ASC";

            using var cmd = new MySqlCommand(sql, conexion);
            new MySqlDataAdapter(cmd).Fill(tabla);

            ViewBag.Categorias = tabla;
        }
    }
}