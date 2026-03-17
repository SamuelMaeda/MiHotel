using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    // ===============================
    // CONTROLADOR DE PERMISOS
    // ===============================
    public class PermisosController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public PermisosController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ===============================
        // VALIDACIONES DE ACCESO
        // ===============================
        private bool SesionActiva()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("IdUsuario"));
        }

        private bool EsAdmin()
        {
            string? nombreRol = HttpContext.Session.GetString("NombreRol");

            return !string.IsNullOrEmpty(nombreRol) &&
                   nombreRol.Equals("admin", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult? RedirigirSiNoTieneAcceso()
        {
            if (!SesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            if (!EsAdmin())
            {
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        // ===============================
        // LISTADO DE PERMISOS
        // ===============================
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            int registrosPorPagina = 20;
            if (pagina < 1) pagina = 1;

            string columnaOrden = ObtenerColumnaOrdenValida(ordenarPor);
            string direccionOrden = direccion?.ToLower() == "desc" ? "desc" : "asc";
            bool verActivos = vista?.ToLower() != "inactivos";

            DataTable tablaPermisos = new DataTable();
            int totalRegistros = 0;
            int totalPaginas = 1;

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                // ===============================
                // TOTAL DE REGISTROS
                // ===============================
                string sqlCount = @"
                    SELECT COUNT(*)
                    FROM permisos
                    WHERE estado = @estado
                      AND (
                            @busqueda = '' OR
                            nombre_permiso LIKE @busquedaLike OR
                            descripcion LIKE @busquedaLike
                          );";

                using (MySqlCommand cmdCount = new MySqlCommand(sqlCount, conexion))
                {
                    cmdCount.Parameters.AddWithValue("@estado", verActivos ? 1 : 0);
                    cmdCount.Parameters.AddWithValue("@busqueda", busqueda ?? "");
                    cmdCount.Parameters.AddWithValue("@busquedaLike", $"%{busqueda}%");

                    totalRegistros = Convert.ToInt32(cmdCount.ExecuteScalar());
                }

                totalPaginas = (int)Math.Ceiling((double)totalRegistros / registrosPorPagina);
                if (totalPaginas < 1) totalPaginas = 1;
                if (pagina > totalPaginas) pagina = totalPaginas;

                int offset = (pagina - 1) * registrosPorPagina;

                // ===============================
                // CONSULTA PRINCIPAL
                // ===============================
                string sql = $@"
                    SELECT 
                        id_permiso,
                        nombre_permiso,
                        descripcion,
                        CASE 
                            WHEN estado = 1 THEN 'Activo'
                            ELSE 'Inactivo'
                        END AS estado
                    FROM permisos
                    WHERE estado = @estado
                      AND (
                            @busqueda = '' OR
                            nombre_permiso LIKE @busquedaLike OR
                            descripcion LIKE @busquedaLike
                          )
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using (MySqlCommand cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@estado", verActivos ? 1 : 0);
                    cmd.Parameters.AddWithValue("@busqueda", busqueda ?? "");
                    cmd.Parameters.AddWithValue("@busquedaLike", $"%{busqueda}%");
                    cmd.Parameters.AddWithValue("@limite", registrosPorPagina);
                    cmd.Parameters.AddWithValue("@offset", offset);

                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(tablaPermisos);
                    }
                }
            }

            ViewBag.Busqueda = busqueda;
            ViewBag.OrdenarPor = ordenarPor;
            ViewBag.Direccion = direccionOrden;
            ViewBag.Vista = verActivos ? "activos" : "inactivos";
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalRegistros = totalRegistros;

            return View(tablaPermisos);
        }

        // ===============================
        // CREAR PERMISO
        // ===============================
        [HttpGet]
        public IActionResult Crear()
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            return View(new Permiso());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(Permiso modelo)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            modelo.NombrePermiso = (modelo.NombrePermiso ?? "").Trim();
            modelo.Descripcion = string.IsNullOrWhiteSpace(modelo.Descripcion)
                ? null
                : modelo.Descripcion.Trim();

            if (!ModelState.IsValid)
                return View(modelo);

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                // ===============================
                // VALIDAR NOMBRE DUPLICADO
                // ===============================
                string sqlExiste = @"
                    SELECT COUNT(*)
                    FROM permisos
                    WHERE LOWER(nombre_permiso) = LOWER(@nombre);";

                using (MySqlCommand cmdExiste = new MySqlCommand(sqlExiste, conexion))
                {
                    cmdExiste.Parameters.AddWithValue("@nombre", modelo.NombrePermiso);

                    int existe = Convert.ToInt32(cmdExiste.ExecuteScalar());

                    if (existe > 0)
                    {
                        ModelState.AddModelError("NombrePermiso", "Ya existe un permiso con ese nombre.");
                        return View(modelo);
                    }
                }

                // ===============================
                // INSERTAR PERMISO
                // ===============================
                string sqlInsert = @"
                    INSERT INTO permisos (nombre_permiso, descripcion, estado)
                    VALUES (@nombre, @descripcion, 1);";

                using (MySqlCommand cmdInsert = new MySqlCommand(sqlInsert, conexion))
                {
                    cmdInsert.Parameters.AddWithValue("@nombre", modelo.NombrePermiso);
                    cmdInsert.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);

                    cmdInsert.ExecuteNonQuery();
                }
            }

            TempData["Exito"] = "Permiso creado correctamente.";
            return RedirectToAction("Index");
        }

        // ===============================
        // EDITAR PERMISO
        // ===============================
        [HttpGet]
        public IActionResult Editar(int id)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            Permiso? permiso = null;

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                string sql = @"
                    SELECT id_permiso, nombre_permiso, descripcion, estado
                    FROM permisos
                    WHERE id_permiso = @id
                    LIMIT 1;";

                using (MySqlCommand cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            permiso = new Permiso
                            {
                                IdPermiso = Convert.ToInt32(reader["id_permiso"]),
                                NombrePermiso = reader["nombre_permiso"]?.ToString() ?? "",
                                Descripcion = reader["descripcion"] == DBNull.Value ? null : reader["descripcion"].ToString(),
                                Estado = Convert.ToInt32(reader["estado"]) == 1
                            };
                        }
                    }
                }
            }

            if (permiso == null)
            {
                TempData["Mensaje"] = "El permiso no fue encontrado.";
                return RedirectToAction("Index");
            }

            return View(permiso);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(Permiso modelo)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            modelo.NombrePermiso = (modelo.NombrePermiso ?? "").Trim();
            modelo.Descripcion = string.IsNullOrWhiteSpace(modelo.Descripcion)
                ? null
                : modelo.Descripcion.Trim();

            if (!ModelState.IsValid)
                return View(modelo);

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                // ===============================
                // VALIDAR NOMBRE DUPLICADO EN OTRO REGISTRO
                // ===============================
                string sqlExiste = @"
                    SELECT COUNT(*)
                    FROM permisos
                    WHERE LOWER(nombre_permiso) = LOWER(@nombre)
                      AND id_permiso <> @id;";

                using (MySqlCommand cmdExiste = new MySqlCommand(sqlExiste, conexion))
                {
                    cmdExiste.Parameters.AddWithValue("@nombre", modelo.NombrePermiso);
                    cmdExiste.Parameters.AddWithValue("@id", modelo.IdPermiso);

                    int existe = Convert.ToInt32(cmdExiste.ExecuteScalar());

                    if (existe > 0)
                    {
                        ModelState.AddModelError("NombrePermiso", "Ya existe otro permiso con ese nombre.");
                        return View(modelo);
                    }
                }

                // ===============================
                // ACTUALIZAR PERMISO
                // ===============================
                string sqlUpdate = @"
                    UPDATE permisos
                    SET nombre_permiso = @nombre,
                        descripcion = @descripcion
                    WHERE id_permiso = @id;";

                using (MySqlCommand cmdUpdate = new MySqlCommand(sqlUpdate, conexion))
                {
                    cmdUpdate.Parameters.AddWithValue("@nombre", modelo.NombrePermiso);
                    cmdUpdate.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@id", modelo.IdPermiso);

                    int filas = cmdUpdate.ExecuteNonQuery();

                    if (filas == 0)
                    {
                        TempData["Mensaje"] = "No se pudo actualizar el permiso.";
                        return RedirectToAction("Index");
                    }
                }
            }

            TempData["Exito"] = "Permiso actualizado correctamente.";
            return RedirectToAction("Index");
        }

        // ===============================
        // CAMBIAR ESTADO DEL PERMISO
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int id, string busqueda = "", string ordenarPor = "nombre", string direccion = "asc", string vista = "activos", int pagina = 1)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            int nuevoEstado = vista == "activos" ? 0 : 1;

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                string sql = @"
                    UPDATE permisos
                    SET estado = @estado
                    WHERE id_permiso = @id;";

                using (MySqlCommand cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                    cmd.Parameters.AddWithValue("@id", id);

                    int filas = cmd.ExecuteNonQuery();

                    if (filas == 0)
                    {
                        TempData["Mensaje"] = "No se pudo cambiar el estado del permiso.";
                        return RedirectToAction("Index", new
                        {
                            busqueda,
                            ordenarPor,
                            direccion,
                            vista,
                            pagina
                        });
                    }
                }
            }

            TempData["Exito"] = nuevoEstado == 1
                ? "Permiso activado correctamente."
                : "Permiso desactivado correctamente.";

            return RedirectToAction("Index", new
            {
                busqueda,
                ordenarPor,
                direccion,
                vista,
                pagina
            });
        }

        // ===============================
        // ORDENAMIENTO SEGURO
        // ===============================
        private string ObtenerColumnaOrdenValida(string? ordenarPor)
        {
            return ordenarPor?.ToLower() switch
            {
                "nombre" => "nombre_permiso",
                "descripcion" => "descripcion",
                "estado" => "estado",
                _ => "nombre_permiso"
            };
        }
    }
}