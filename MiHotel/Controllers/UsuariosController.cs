// ===============================
// CONTROLADOR DE USUARIOS
// ===============================

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Utilidades;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public UsuariosController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ===============================
        // VALIDAR SESION ACTIVA
        // ===============================

        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrEmpty(idUsuario);
        }

        // ===============================
        // VALIDAR SI EL USUARIO ES ADMIN
        // ===============================

        private bool EsAdmin()
        {
            string? nombreRol = HttpContext.Session.GetString("NombreRol");

            if (string.IsNullOrEmpty(nombreRol))
            {
                return false;
            }

            return nombreRol.Trim().ToLower() == "admin";
        }

        // ===============================
        // VALIDAR ACCESO AL MODULO
        // ===============================

        private IActionResult? ValidarAccesoAdmin()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            if (!EsAdmin())
            {
                TempData["Mensaje"] = "No tiene permisos para acceder al módulo de usuarios.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        // ===============================
        // NORMALIZAR TELEFONO
        // ===============================

        private string NormalizarTelefono(string telefono)
        {
            return telefono.Replace(" ", "").Trim();
        }

        // ===============================
        // FORMATEAR TELEFONO
        // ===============================

        private string FormatearTelefono(string telefono)
        {
            string telefonoLimpio = NormalizarTelefono(telefono);

            if (telefonoLimpio.Length == 8)
            {
                return telefonoLimpio.Substring(0, 4) + " " + telefonoLimpio.Substring(4, 4);
            }

            return telefono;
        }

        // ===============================
        // OBTENER COLUMNA SQL SEGURA
        // ===============================

        private string ObtenerColumnaOrden(string columna)
        {
            return columna.ToLower() switch
            {
                "nombre" => "u.nombre_usuario",
                "correo" => "u.correo",
                "rol" => "r.nombre_rol",
                _ => "u.id_usuario"
            };
        }

        // ===============================
        // LISTADO DE USUARIOS
        // ===============================

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string columna = "id",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaUsuarios = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string vistaNormalizada = vista.Trim().ToLower() == "inactivos" ? "inactivos" : "activos";
                string columnaOrden = ObtenerColumnaOrden(columna);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string condicionEstado = vistaNormalizada == "inactivos"
                    ? "u.estado = 'inactivo'"
                    : "u.estado = 'activo'";

                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    string columnaBusqueda = columna.Trim().ToLower() switch
                    {
                        "nombre" => "u.nombre_usuario",
                        "correo" => "u.correo",
                        "rol" => "r.nombre_rol",
                        _ => "CAST(u.id_usuario AS CHAR)"
                    };

                    condicionBusqueda = $" AND {columnaBusqueda} LIKE @busqueda ";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM usuario u
                    INNER JOIN rol r ON u.id_rol = r.id_rol
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
                        u.id_usuario,
                        u.nombre_usuario,
                        u.correo,
                        u.telefono,
                        u.estado,
                        r.nombre_rol
                    FROM usuario u
                    INNER JOIN rol r ON u.id_rol = r.id_rol
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
                adaptador.Fill(tablaUsuarios);

                ViewBag.Busqueda = busqueda;
                ViewBag.Columna = columna;
                ViewBag.Direccion = direccionOrden.ToLower();
                ViewBag.Vista = vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar los usuarios: " + ex.Message;
            }

            return View(tablaUsuarios);
        }

        // ===============================
        // VISTA DE CREAR USUARIO
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            CargarRoles();
            return View();
        }

        // ===============================
        // GUARDAR NUEVO USUARIO
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(UsuarioAdmin modelo)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            CargarRoles();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                modelo.Telefono = NormalizarTelefono(modelo.Telefono);

                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string verificarCorreo = @"
                    SELECT COUNT(*)
                    FROM usuario
                    WHERE correo = @correo;";

                using var comandoVerificar = new MySqlCommand(verificarCorreo, conexion);
                comandoVerificar.Parameters.AddWithValue("@correo", modelo.Correo);

                int existe = Convert.ToInt32(comandoVerificar.ExecuteScalar());

                if (existe > 0)
                {
                    ViewBag.Mensaje = "El correo ya está registrado.";
                    modelo.Telefono = FormatearTelefono(modelo.Telefono);
                    return View(modelo);
                }

                string claveHash = SeguridadHelper.ObtenerSha256(modelo.Clave);

                string insertar = @"
                    INSERT INTO usuario
                    (
                        id_rol,
                        nombre_usuario,
                        correo,
                        telefono,
                        clave,
                        estado
                    )
                    VALUES
                    (
                        @id_rol,
                        @nombre,
                        @correo,
                        @telefono,
                        @clave,
                        @estado
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_rol", modelo.IdRol);
                comandoInsertar.Parameters.AddWithValue("@nombre", modelo.Nombre);
                comandoInsertar.Parameters.AddWithValue("@correo", modelo.Correo);
                comandoInsertar.Parameters.AddWithValue("@telefono", modelo.Telefono);
                comandoInsertar.Parameters.AddWithValue("@clave", claveHash);
                comandoInsertar.Parameters.AddWithValue("@estado", modelo.Estado);

                comandoInsertar.ExecuteNonQuery();

                TempData["Exito"] = "Usuario creado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar el usuario: " + ex.Message;
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                return View(modelo);
            }
        }

        // ===============================
        // VISTA DE EDITAR USUARIO
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            CargarRoles();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        id_usuario,
                        id_rol,
                        nombre_usuario,
                        correo,
                        telefono,
                        estado
                    FROM usuario
                    WHERE id_usuario = @id
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el usuario solicitado.";
                    return RedirectToAction("Index");
                }

                EditarUsuario modelo = new EditarUsuario
                {
                    IdUsuario = Convert.ToInt32(lector["id_usuario"]),
                    IdRol = Convert.ToInt32(lector["id_rol"]),
                    Nombre = lector["nombre_usuario"]?.ToString() ?? "",
                    Correo = lector["correo"]?.ToString() ?? "",
                    Telefono = FormatearTelefono(lector["telefono"]?.ToString() ?? ""),
                    Estado = lector["estado"]?.ToString() ?? "activo"
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el usuario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ===============================
        // GUARDAR EDICION DE USUARIO
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(EditarUsuario modelo)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null)
            {
                return acceso;
            }

            CargarRoles();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                modelo.Telefono = NormalizarTelefono(modelo.Telefono);

                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string verificarCorreo = @"
                    SELECT COUNT(*)
                    FROM usuario
                    WHERE correo = @correo
                    AND id_usuario <> @id_usuario;";

                using var comandoVerificar = new MySqlCommand(verificarCorreo, conexion);
                comandoVerificar.Parameters.AddWithValue("@correo", modelo.Correo);
                comandoVerificar.Parameters.AddWithValue("@id_usuario", modelo.IdUsuario);

                int existe = Convert.ToInt32(comandoVerificar.ExecuteScalar());

                if (existe > 0)
                {
                    ViewBag.Mensaje = "El correo ya está registrado por otro usuario.";
                    modelo.Telefono = FormatearTelefono(modelo.Telefono);
                    return View(modelo);
                }

                string actualizar = @"
                    UPDATE usuario
                    SET id_rol = @id_rol,
                        nombre_usuario = @nombre,
                        correo = @correo,
                        telefono = @telefono,
                        estado = @estado
                    WHERE id_usuario = @id_usuario;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@id_rol", modelo.IdRol);
                comandoActualizar.Parameters.AddWithValue("@nombre", modelo.Nombre);
                comandoActualizar.Parameters.AddWithValue("@correo", modelo.Correo);
                comandoActualizar.Parameters.AddWithValue("@telefono", modelo.Telefono);
                comandoActualizar.Parameters.AddWithValue("@estado", modelo.Estado);
                comandoActualizar.Parameters.AddWithValue("@id_usuario", modelo.IdUsuario);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Usuario actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar el usuario: " + ex.Message;
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                return View(modelo);
            }
        }

        // ===============================
        // CAMBIAR ESTADO DE USUARIO
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CambiarEstado(
            int id,
            string busqueda = "",
            string columna = "id",
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

                string obtenerEstado = @"
                    SELECT estado
                    FROM usuario
                    WHERE id_usuario = @id
                    LIMIT 1;";

                using var comandoEstado = new MySqlCommand(obtenerEstado, conexion);
                comandoEstado.Parameters.AddWithValue("@id", id);

                object? resultadoEstado = comandoEstado.ExecuteScalar();

                if (resultadoEstado == null)
                {
                    TempData["Mensaje"] = "No se encontró el usuario solicitado.";
                    return RedirectToAction("Index", new { busqueda, columna, direccion, vista, pagina });
                }

                string estadoActual = resultadoEstado.ToString()?.Trim().ToLower() ?? "inactivo";
                string nuevoEstado = estadoActual == "activo" ? "inactivo" : "activo";

                string actualizarEstado = @"
                    UPDATE usuario
                    SET estado = @estado
                    WHERE id_usuario = @id;";

                using var comandoActualizar = new MySqlCommand(actualizarEstado, conexion);
                comandoActualizar.Parameters.AddWithValue("@estado", nuevoEstado);
                comandoActualizar.Parameters.AddWithValue("@id", id);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Estado del usuario actualizado correctamente.";
                return RedirectToAction("Index", new { busqueda, columna, direccion, vista, pagina });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cambiar el estado: " + ex.Message;
                return RedirectToAction("Index", new { busqueda, columna, direccion, vista, pagina });
            }
        }

        // ===============================
        // VISTA DE RESETEO DE CLAVE
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult ResetearClave(int id)
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
                    SELECT id_usuario, nombre_usuario
                    FROM usuario
                    WHERE id_usuario = @id
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el usuario solicitado.";
                    return RedirectToAction("Index");
                }

                ResetearClaveAdmin modelo = new ResetearClaveAdmin
                {
                    IdUsuario = Convert.ToInt32(lector["id_usuario"]),
                    NombreUsuario = lector["nombre_usuario"]?.ToString() ?? ""
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar la pantalla de reseteo: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ===============================
        // GUARDAR NUEVA CLAVE
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult ResetearClave(ResetearClaveAdmin modelo)
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

                string claveHash = SeguridadHelper.ObtenerSha256(modelo.NuevaClave);

                string actualizar = @"
                    UPDATE usuario
                    SET clave = @clave
                    WHERE id_usuario = @id_usuario;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@clave", claveHash);
                comandoActualizar.Parameters.AddWithValue("@id_usuario", modelo.IdUsuario);

                int filasAfectadas = comandoActualizar.ExecuteNonQuery();

                if (filasAfectadas == 0)
                {
                    ViewBag.Mensaje = "No se pudo actualizar la contraseña del usuario.";
                    return View(modelo);
                }

                TempData["Exito"] = "Contraseña restablecida correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al restablecer la contraseña: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // CARGAR ROLES DESDE BASE DE DATOS
        // ===============================

        private void CargarRoles()
        {
            List<dynamic> listaRoles = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT id_rol, nombre_rol
                    FROM rol
                    ORDER BY nombre_rol;";

                using var comando = new MySqlCommand(consulta, conexion);
                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    listaRoles.Add(new
                    {
                        IdRol = Convert.ToInt32(lector["id_rol"]),
                        NombreRol = lector["nombre_rol"]?.ToString() ?? ""
                    });
                }
            }
            catch
            {
                // Si ocurre un error cargando roles,
                // la vista simplemente mostrará la lista vacía.
            }

            ViewBag.Roles = listaRoles;
        }
    }
}