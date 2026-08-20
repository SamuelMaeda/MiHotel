using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class MantenimientosController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public MantenimientosController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarAcceso()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("IdUsuario")))
                return RedirectToAction("Login", "Acceso");

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
            return rol is "admin" or "recepcionista" ? null : RedirectToAction("Index", "Panel");
        }

        private static string ColumnaOrden(string columna) => columna.ToLower() switch
        {
            "nombre" => "nombre",
            "descripcion" => "descripcion",
            "completado" => "fecha_completado",
            _ => "fecha_registro"
        };

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string vista = "pendientes",
            string busqueda = "",
            string ordenarPor = "fecha",
            string direccion = "desc",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            DataTable tabla = new();
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                string vistaNormalizada = vista == "historial" ? "historial" : "pendientes";
                string estado = vistaNormalizada == "historial" ? "completado" : "pendiente";
                string columna = ColumnaOrden(ordenarPor);
                string sentido = direccion.ToLower() == "asc" ? "ASC" : "DESC";
                pagina = Math.Max(1, pagina);
                string filtro = string.IsNullOrWhiteSpace(busqueda)
                    ? ""
                    : " AND (nombre LIKE @busqueda OR descripcion LIKE @busqueda)";

                using var conteo = new MySqlCommand(
                    "SELECT COUNT(*) FROM mantenimiento WHERE estado=@estado" + filtro + ";", conexion);
                conteo.Parameters.AddWithValue("@estado", estado);
                if (filtro.Length > 0) conteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                int total = Convert.ToInt32(conteo.ExecuteScalar());
                int paginas = Math.Max(1, (int)Math.Ceiling(total / (double)RegistrosPorPagina));
                pagina = Math.Min(pagina, paginas);

                string sql = $@"
                    SELECT id_mantenimiento,nombre,descripcion,fecha_registro,fecha_completado,estado
                    FROM mantenimiento
                    WHERE estado=@estado {filtro}
                    ORDER BY {columna} {sentido}
                    LIMIT @limite OFFSET @offset;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@estado", estado);
                if (filtro.Length > 0) comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", (pagina - 1) * RegistrosPorPagina);
                new MySqlDataAdapter(comando).Fill(tabla);

                ViewBag.Vista = vistaNormalizada;
                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = sentido.ToLower();
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = paginas;
                ViewBag.TotalRegistros = total;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar los mantenimientos: " + ex.Message;
            }
            return View(tabla);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAcceso();
            return acceso ?? View(new MantenimientoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(MantenimientoViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (!ModelState.IsValid) return View(modelo);

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand(@"
                    INSERT INTO mantenimiento (nombre,descripcion,fecha_registro,fecha_completado,estado)
                    VALUES (@nombre,@descripcion,CURRENT_TIMESTAMP,NULL,'pendiente');", conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@descripcion", modelo.Descripcion.Trim());
                comando.ExecuteNonQuery();
                TempData["Exito"] = "Mantenimiento agregado a la lista de pendientes.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al registrar el mantenimiento: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpGet]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand(@"
                    SELECT id_mantenimiento,nombre,descripcion,fecha_registro,estado
                    FROM mantenimiento
                    WHERE id_mantenimiento=@id AND estado='pendiente'
                    LIMIT 1;", conexion);
                comando.Parameters.AddWithValue("@id", id);
                using var lector = comando.ExecuteReader();
                if (!lector.Read())
                {
                    TempData["Mensaje"] = "El mantenimiento no existe o ya fue completado.";
                    return RedirectToAction("Index");
                }
                return View(new MantenimientoViewModel
                {
                    IdMantenimiento = id,
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    Descripcion = lector["descripcion"]?.ToString() ?? "",
                    FechaRegistro = Convert.ToDateTime(lector["fecha_registro"]),
                    Estado = lector["estado"]?.ToString() ?? "pendiente"
                });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el mantenimiento: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(MantenimientoViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (!ModelState.IsValid) return View(modelo);

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand(@"
                    UPDATE mantenimiento SET nombre=@nombre,descripcion=@descripcion
                    WHERE id_mantenimiento=@id AND estado='pendiente';", conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@descripcion", modelo.Descripcion.Trim());
                comando.Parameters.AddWithValue("@id", modelo.IdMantenimiento);
                if (comando.ExecuteNonQuery() == 0)
                {
                    TempData["Mensaje"] = "El mantenimiento no existe o ya fue completado.";
                    return RedirectToAction("Index");
                }
                TempData["Exito"] = "Mantenimiento actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar el mantenimiento: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Completar(int id, string busqueda = "", string ordenarPor = "fecha", string direccion = "desc", int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand(@"
                    UPDATE mantenimiento
                    SET estado='completado',fecha_completado=CURRENT_TIMESTAMP
                    WHERE id_mantenimiento=@id AND estado='pendiente';", conexion);
                comando.Parameters.AddWithValue("@id", id);
                if (comando.ExecuteNonQuery() == 0)
                    TempData["Mensaje"] = "El mantenimiento no existe o ya fue completado.";
                else
                    TempData["Exito"] = "Mantenimiento completado y trasladado al historial.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al completar el mantenimiento: " + ex.Message;
            }
            return RedirectToAction("Index", new { vista = "pendientes", busqueda, ordenarPor, direccion, pagina });
        }
    }
}
