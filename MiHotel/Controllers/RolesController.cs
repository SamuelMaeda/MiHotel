// ===============================
// CONTROLADOR DE ROLES
// ===============================

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class RolesController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public RolesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrEmpty(idUsuario);
        }

        private bool EsAdmin()
        {
            string? nombreRol = HttpContext.Session.GetString("NombreRol");

            if (string.IsNullOrEmpty(nombreRol))
            {
                return false;
            }

            return nombreRol.Trim().ToLower() == "admin";
        }

        private IActionResult? ValidarAccesoAdmin()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            if (!EsAdmin())
            {
                TempData["Mensaje"] = "No tiene permisos para acceder al módulo de roles.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        private string ObtenerColumnaOrden(string columna)
        {
            return columna.ToLower() switch
            {
                "estado" => "estado",
                _ => "nombre_rol"
            };
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaRoles = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string vistaNormalizada = vista.Trim().ToLower() == "inactivos" ? "inactivos" : "activos";
                string columnaOrden = ObtenerColumnaOrden(ordenarPor);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string condicionEstado = vistaNormalizada == "inactivos"
                    ? "estado = 'inactivo'"
                    : "estado = 'activo'";

                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    condicionBusqueda = @"
                        AND (
                            nombre_rol LIKE @busqueda
                            OR estado LIKE @busqueda
                        ) ";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM rol
                    WHERE {condicionEstado}
                    {condicionBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comandoConteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                int totalRegistros = Convert.ToInt32(comandoConteo.ExecuteScalar());
                int totalPaginas = (int)Math.Ceiling((double)totalRegistros / RegistrosPorPagina);

                if (totalPaginas == 0)
                {
                    totalPaginas = 1;
                }

                if (pagina > totalPaginas)
                {
                    pagina = totalPaginas;
                }

                int offset = (pagina - 1) * RegistrosPorPagina;

                string consulta = $@"
                    SELECT
                        id_rol,
                        nombre_rol,
                        estado
                    FROM rol
                    WHERE {condicionEstado}
                    {condicionBusqueda}
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tablaRoles);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = direccionOrden.ToLower();
                ViewBag.Vista = vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al cargar los roles: " + ex.Message;
            }

            return View(tablaRoles);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            return View();
        }

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(Rol modelo)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string verificar = @"
                    SELECT COUNT(*)
                    FROM rol
                    WHERE nombre_rol = @nombre;";

                using var comandoVerificar = new MySqlCommand(verificar, conexion);
                comandoVerificar.Parameters.AddWithValue("@nombre", modelo.NombreRol.Trim());

                int existe = Convert.ToInt32(comandoVerificar.ExecuteScalar());

                if (existe > 0)
                {
                    ViewBag.Mensaje = "Ya existe un rol con ese nombre.";
                    return View(modelo);
                }

                string insertar = @"
                    INSERT INTO rol
                    (
                        nombre_rol,
                        estado
                    )
                    VALUES
                    (
                        @nombre,
                        @estado
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@nombre", modelo.NombreRol.Trim());
                comandoInsertar.Parameters.AddWithValue("@estado", modelo.Estado);

                comandoInsertar.ExecuteNonQuery();

                TempData["Exito"] = "Rol creado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al crear el rol: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        id_rol,
                        nombre_rol,
                        estado
                    FROM rol
                    WHERE id_rol = @id
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "El rol no existe.";
                    return RedirectToAction("Index");
                }

                Rol modelo = new Rol
                {
                    IdRol = Convert.ToInt32(lector["id_rol"]),
                    NombreRol = lector["nombre_rol"]?.ToString() ?? "",
                    Estado = lector["estado"]?.ToString() ?? "activo"
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cargar el rol: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(Rol modelo)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string verificar = @"
                    SELECT COUNT(*)
                    FROM rol
                    WHERE nombre_rol = @nombre
                      AND id_rol <> @id;";

                using var comandoVerificar = new MySqlCommand(verificar, conexion);
                comandoVerificar.Parameters.AddWithValue("@nombre", modelo.NombreRol.Trim());
                comandoVerificar.Parameters.AddWithValue("@id", modelo.IdRol);

                int existe = Convert.ToInt32(comandoVerificar.ExecuteScalar());

                if (existe > 0)
                {
                    ViewBag.Mensaje = "Ya existe otro rol con ese nombre.";
                    return View(modelo);
                }

                string actualizar = @"
                    UPDATE rol
                    SET
                        nombre_rol = @nombre,
                        estado = @estado
                    WHERE id_rol = @id;";

                using var comando = new MySqlCommand(actualizar, conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.NombreRol.Trim());
                comando.Parameters.AddWithValue("@estado", modelo.Estado);
                comando.Parameters.AddWithValue("@id", modelo.IdRol);

                comando.ExecuteNonQuery();

                TempData["Exito"] = "Rol actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al actualizar el rol: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CambiarEstado(
            int id,
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT estado
                    FROM rol
                    WHERE id_rol = @id
                    LIMIT 1;";

                using var comandoConsulta = new MySqlCommand(consulta, conexion);
                comandoConsulta.Parameters.AddWithValue("@id", id);

                object? resultado = comandoConsulta.ExecuteScalar();

                if (resultado == null)
                {
                    TempData["Mensaje"] = "El rol no existe.";
                    return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
                }

                string estadoActual = resultado.ToString()?.Trim().ToLower() ?? "activo";
                string nuevoEstado = estadoActual == "activo" ? "inactivo" : "activo";

                string actualizar = @"
                    UPDATE rol
                    SET estado = @estado
                    WHERE id_rol = @id;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@estado", nuevoEstado);
                comandoActualizar.Parameters.AddWithValue("@id", id);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Estado del rol actualizado.";
                return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cambiar el estado: " + ex.Message;
                return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
            }
        }
    }
}