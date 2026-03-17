using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    // ===============================
    // CONTROLADOR DE ROL PERMISO
    // ===============================
    public class RolPermisosController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public RolPermisosController(ConexionBD conexionBD)
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
        // LISTADO DE ROLES CONFIGURABLES
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

            DataTable tablaRoles = new DataTable();
            int totalRegistros = 0;
            int totalPaginas = 1;

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                string sqlCount = @"
                    SELECT COUNT(*)
                    FROM rol
                    WHERE LOWER(nombre_rol) <> 'admin'
                      AND estado = @estado
                      AND (
                            @busqueda = '' OR
                            nombre_rol LIKE @busquedaLike
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

                string sql = $@"
                    SELECT 
                        id_rol,
                        nombre_rol,
                        CASE
                            WHEN estado = 1 THEN 'Activo'
                            ELSE 'Inactivo'
                        END AS estado
                    FROM rol
                    WHERE LOWER(nombre_rol) <> 'admin'
                      AND estado = @estado
                      AND (
                            @busqueda = '' OR
                            nombre_rol LIKE @busquedaLike
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
                        adapter.Fill(tablaRoles);
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

            return View(tablaRoles);
        }

        // ===============================
        // GESTIONAR PERMISOS DE UN ROL
        // ===============================
        [HttpGet]
        public IActionResult Gestionar(int id)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            GestionRolPermisoViewModel modelo = new GestionRolPermisoViewModel();

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                string sqlRol = @"
                    SELECT id_rol, nombre_rol
                    FROM rol
                    WHERE id_rol = @id
                      AND LOWER(nombre_rol) <> 'admin'
                    LIMIT 1;";

                using (MySqlCommand cmdRol = new MySqlCommand(sqlRol, conexion))
                {
                    cmdRol.Parameters.AddWithValue("@id", id);

                    using (MySqlDataReader reader = cmdRol.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            modelo.IdRol = Convert.ToInt32(reader["id_rol"]);
                            modelo.NombreRol = reader["nombre_rol"]?.ToString() ?? "";
                        }
                    }
                }

                if (modelo.IdRol == 0)
                {
                    TempData["Mensaje"] = "El rol no fue encontrado o no se puede configurar.";
                    return RedirectToAction("Index");
                }

                string sqlPermisos = @"
                    SELECT 
                        p.id_permiso,
                        p.nombre_permiso,
                        p.descripcion,
                        CASE 
                            WHEN rp.id_rol IS NULL THEN 0
                            ELSE 1
                        END AS asignado
                    FROM permisos p
                    LEFT JOIN rol_permiso rp 
                        ON rp.id_permiso = p.id_permiso
                       AND rp.id_rol = @idRol
                    WHERE p.estado = 1
                    ORDER BY p.nombre_permiso ASC;";

                using (MySqlCommand cmdPermisos = new MySqlCommand(sqlPermisos, conexion))
                {
                    cmdPermisos.Parameters.AddWithValue("@idRol", modelo.IdRol);

                    using (MySqlDataReader reader = cmdPermisos.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            modelo.Permisos.Add(new PermisoAsignadoViewModel
                            {
                                IdPermiso = Convert.ToInt32(reader["id_permiso"]),
                                NombrePermiso = reader["nombre_permiso"]?.ToString() ?? "",
                                Descripcion = reader["descripcion"] == DBNull.Value ? null : reader["descripcion"].ToString(),
                                Asignado = Convert.ToInt32(reader["asignado"]) == 1
                            });
                        }
                    }
                }
            }

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Gestionar(GestionRolPermisoViewModel modelo)
        {
            var acceso = RedirigirSiNoTieneAcceso();
            if (acceso != null)
                return acceso;

            if (modelo.IdRol <= 0)
            {
                TempData["Mensaje"] = "Rol no válido.";
                return RedirectToAction("Index");
            }

            using (MySqlConnection conexion = _conexionBD.ObtenerConexion())
            {
                conexion.Open();

                string sqlValidarRol = @"
                    SELECT COUNT(*)
                    FROM rol
                    WHERE id_rol = @id
                      AND LOWER(nombre_rol) <> 'admin';";

                using (MySqlCommand cmdValidar = new MySqlCommand(sqlValidarRol, conexion))
                {
                    cmdValidar.Parameters.AddWithValue("@id", modelo.IdRol);

                    int existe = Convert.ToInt32(cmdValidar.ExecuteScalar());
                    if (existe == 0)
                    {
                        TempData["Mensaje"] = "El rol no fue encontrado o no se puede configurar.";
                        return RedirectToAction("Index");
                    }
                }

                using (MySqlTransaction transaccion = conexion.BeginTransaction())
                {
                    try
                    {
                        string sqlEliminar = @"
                            DELETE FROM rol_permiso
                            WHERE id_rol = @idRol;";

                        using (MySqlCommand cmdEliminar = new MySqlCommand(sqlEliminar, conexion, transaccion))
                        {
                            cmdEliminar.Parameters.AddWithValue("@idRol", modelo.IdRol);
                            cmdEliminar.ExecuteNonQuery();
                        }

                        if (modelo.Permisos != null)
                        {
                            foreach (var permiso in modelo.Permisos.Where(p => p.Asignado))
                            {
                                string sqlInsertar = @"
                                    INSERT INTO rol_permiso (id_rol, id_permiso)
                                    VALUES (@idRol, @idPermiso);";

                                using (MySqlCommand cmdInsertar = new MySqlCommand(sqlInsertar, conexion, transaccion))
                                {
                                    cmdInsertar.Parameters.AddWithValue("@idRol", modelo.IdRol);
                                    cmdInsertar.Parameters.AddWithValue("@idPermiso", permiso.IdPermiso);
                                    cmdInsertar.ExecuteNonQuery();
                                }
                            }
                        }

                        transaccion.Commit();
                    }
                    catch
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se pudieron guardar los permisos del rol.";
                        return RedirectToAction("Gestionar", new { id = modelo.IdRol });
                    }
                }
            }

            TempData["Exito"] = "Permisos del rol actualizados correctamente.";
            return RedirectToAction("Index");
        }

        // ===============================
        // ORDENAMIENTO SEGURO
        // ===============================
        private string ObtenerColumnaOrdenValida(string? ordenarPor)
        {
            return ordenarPor?.ToLower() switch
            {
                "nombre" => "nombre_rol",
                "estado" => "estado",
                _ => "nombre_rol"
            };
        }
    }
}