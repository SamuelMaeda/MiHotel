using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class TrabajadoresController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public TrabajadoresController(ConexionBD conexionBD)
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

        private static string NormalizarTelefono(string telefono) => telefono.Replace(" ", "").Trim();

        private static string FormatearTelefono(string telefono)
        {
            string limpio = NormalizarTelefono(telefono);
            return limpio.Length == 8 ? limpio[..4] + " " + limpio[4..] : telefono;
        }

        private static string? NormalizarOpcional(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        private static string ColumnaOrden(string columna) => columna.ToLower() switch
        {
            "codigo" => "codigo_servicio",
            "telefono" => "telefono",
            "observaciones" => "observaciones",
            _ => "nombre"
        };

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string vista = "activos",
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            DataTable tabla = new();
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                string vistaNormalizada = vista == "inactivos" ? "inactivos" : "activos";
                string estado = vistaNormalizada == "inactivos" ? "inactivo" : "activo";
                string columna = ColumnaOrden(ordenarPor);
                string sentido = direccion.ToLower() == "desc" ? "DESC" : "ASC";
                pagina = Math.Max(1, pagina);
                string filtro = string.IsNullOrWhiteSpace(busqueda) ? "" : @"
                    AND (nombre LIKE @busqueda OR codigo_servicio LIKE @busqueda
                         OR telefono LIKE @busqueda OR observaciones LIKE @busqueda)";

                using var conteo = new MySqlCommand(
                    "SELECT COUNT(*) FROM trabajador WHERE estado=@estado " + filtro + ";", conexion);
                conteo.Parameters.AddWithValue("@estado", estado);
                if (filtro.Length > 0) conteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                int total = Convert.ToInt32(conteo.ExecuteScalar());
                int paginas = Math.Max(1, (int)Math.Ceiling(total / (double)RegistrosPorPagina));
                pagina = Math.Min(pagina, paginas);

                string sql = $@"
                    SELECT id_trabajador,nombre,codigo_servicio,telefono,observaciones,estado
                    FROM trabajador
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
                ViewBag.Mensaje = "Ocurrió un error al cargar los trabajadores: " + ex.Message;
            }
            return View(tabla);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAcceso();
            return acceso ?? View(new TrabajadorViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(TrabajadorViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (!ModelState.IsValid) return View(modelo);

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                const string sql = @"
                    INSERT INTO trabajador (nombre,codigo_servicio,telefono,observaciones,estado)
                    VALUES (@nombre,@codigo,@telefono,@observaciones,'activo');";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@codigo", (object?)NormalizarOpcional(modelo.CodigoServicio)?.ToUpperInvariant() ?? DBNull.Value);
                comando.Parameters.AddWithValue("@telefono", NormalizarTelefono(modelo.Telefono));
                comando.Parameters.AddWithValue("@observaciones", (object?)NormalizarOpcional(modelo.Observaciones) ?? DBNull.Value);
                comando.ExecuteNonQuery();
                TempData["Exito"] = "Trabajador registrado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar el trabajador: " + ex.Message;
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
                    SELECT id_trabajador,nombre,codigo_servicio,telefono,observaciones,estado
                    FROM trabajador WHERE id_trabajador=@id LIMIT 1;", conexion);
                comando.Parameters.AddWithValue("@id", id);
                using var lector = comando.ExecuteReader();
                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el trabajador solicitado.";
                    return RedirectToAction("Index");
                }
                return View(new TrabajadorViewModel
                {
                    IdTrabajador = id,
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    CodigoServicio = lector["codigo_servicio"] == DBNull.Value ? null : lector["codigo_servicio"].ToString(),
                    Telefono = FormatearTelefono(lector["telefono"]?.ToString() ?? ""),
                    Observaciones = lector["observaciones"] == DBNull.Value ? null : lector["observaciones"].ToString(),
                    Estado = lector["estado"]?.ToString() ?? "activo"
                });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el trabajador: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(TrabajadorViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (!ModelState.IsValid) return View(modelo);
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                const string sql = @"
                    UPDATE trabajador
                    SET nombre=@nombre,codigo_servicio=@codigo,telefono=@telefono,observaciones=@observaciones
                    WHERE id_trabajador=@id;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@codigo", (object?)NormalizarOpcional(modelo.CodigoServicio)?.ToUpperInvariant() ?? DBNull.Value);
                comando.Parameters.AddWithValue("@telefono", NormalizarTelefono(modelo.Telefono));
                comando.Parameters.AddWithValue("@observaciones", (object?)NormalizarOpcional(modelo.Observaciones) ?? DBNull.Value);
                comando.Parameters.AddWithValue("@id", modelo.IdTrabajador);
                if (comando.ExecuteNonQuery() == 0)
                {
                    TempData["Mensaje"] = "No se encontró el trabajador solicitado.";
                    return RedirectToAction("Index");
                }
                TempData["Exito"] = "Trabajador actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar el trabajador: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int id, string vista = "activos", string busqueda = "", string ordenarPor = "nombre", string direccion = "asc", int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand(@"
                    UPDATE trabajador SET estado=IF(estado='activo','inactivo','activo')
                    WHERE id_trabajador=@id;", conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.ExecuteNonQuery();
                TempData["Exito"] = "Estado del trabajador actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cambiar el estado: " + ex.Message;
            }
            return RedirectToAction("Index", new { vista, busqueda, ordenarPor, direccion, pagina });
        }
    }
}
