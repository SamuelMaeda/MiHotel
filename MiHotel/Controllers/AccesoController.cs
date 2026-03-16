// ===============================
// CONTROLADOR DE ACCESO
// ===============================

using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Models.Configuracion;
using MiHotel.Utilidades;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class AccesoController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly ConfigSistema _configSistema;

        public AccesoController(
            ConexionBD conexionBD,
            IOptions<ConfigSistema> opcionesConfig)
        {
            _conexionBD = conexionBD;
            _configSistema = opcionesConfig.Value;
        }

        // ===============================
        // CARGA DE DATOS DE CONFIGURACION
        // ===============================

        private void CargarDatosConfiguracion()
        {
            ViewBag.EmpresaNombre = _configSistema.Empresa.Nombre;
            ViewBag.EmpresaLogo = _configSistema.Empresa.Logo;
        }

        // ===============================
        // GENERAR TOKEN SEGURO
        // ===============================

        private string GenerarTokenSeguro()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        // ===============================
        // VISTA DE LOGIN
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("IdUsuario") != null)
            {
                return RedirectToAction("Index", "Panel");
            }

            // ===============================
            // VALIDAR COOKIE DE RECORDAR SESION
            // ===============================

            if (Request.Cookies.TryGetValue("MiHotel_Recordarme", out string? tokenCookie))
            {
                try
                {
                    using var conexion = _conexionBD.ObtenerConexion();
                    conexion.Open();

                    string consultaToken = @"
                        SELECT 
                            u.id_usuario,
                            u.nombre_usuario,
                            u.id_rol,
                            u.estado,
                            u.fecha_expiracion_recordarme,
                            r.nombre_rol
                        FROM usuario u
                        INNER JOIN rol r ON u.id_rol = r.id_rol
                        WHERE u.token_recordarme = @token
                        LIMIT 1;";

                    using var comandoToken = new MySqlCommand(consultaToken, conexion);
                    comandoToken.Parameters.AddWithValue("@token", tokenCookie);

                    using var lector = comandoToken.ExecuteReader();

                    if (lector.Read())
                    {
                        string idUsuario = lector["id_usuario"]?.ToString() ?? "";
                        string nombreUsuario = lector["nombre_usuario"]?.ToString() ?? "";
                        string idRol = lector["id_rol"]?.ToString() ?? "";
                        string nombreRol = lector["nombre_rol"]?.ToString() ?? "";
                        string estado = lector["estado"]?.ToString() ?? "";

                        DateTime fechaExpiracion = Convert.ToDateTime(lector["fecha_expiracion_recordarme"]);

                        bool usuarioActivo = estado.Trim().ToLower() == "activo";
                        bool esCliente = nombreRol.Trim().ToLower() == "cliente";
                        bool tokenVigente = fechaExpiracion >= DateTime.Now;

                        if (usuarioActivo && esCliente && tokenVigente)
                        {
                            HttpContext.Session.SetString("IdUsuario", idUsuario);
                            HttpContext.Session.SetString("NombreUsuario", nombreUsuario);
                            HttpContext.Session.SetString("IdRol", idRol);
                            HttpContext.Session.SetString("NombreRol", nombreRol);

                            return RedirectToAction("Index", "Panel");
                        }
                    }
                }
                catch
                {
                    // Si ocurre un error, no interrumpimos el flujo.
                    // Simplemente se mostrará el login normal.
                }

                // ===============================
                // ELIMINAR COOKIE INVALIDA
                // ===============================

                Response.Cookies.Delete("MiHotel_Recordarme");
            }

            CargarDatosConfiguracion();
            return View();
        }

        // ===============================
        // VALIDACION DE LOGIN
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login(InicioSesion modelo)
        {
            CargarDatosConfiguracion();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT 
                        u.id_usuario,
                        u.nombre_usuario,
                        u.correo,
                        u.clave,
                        u.estado,
                        u.id_rol,
                        r.nombre_rol
                    FROM usuario u
                    INNER JOIN rol r ON u.id_rol = r.id_rol
                    WHERE u.correo = @correo
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@correo", modelo.Correo);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }

                string claveBD = lector["clave"]?.ToString() ?? "";
                string estado = lector["estado"]?.ToString() ?? "";
                string nombreUsuario = lector["nombre_usuario"]?.ToString() ?? "";
                string idUsuario = lector["id_usuario"]?.ToString() ?? "";
                string idRol = lector["id_rol"]?.ToString() ?? "";
                string nombreRol = lector["nombre_rol"]?.ToString() ?? "";

                if (estado.Trim().ToLower() != "activo")
                {
                    ViewBag.Mensaje = "El usuario está inactivo.";
                    return View(modelo);
                }

                string claveIngresadaHash = SeguridadHelper.ObtenerSha256(modelo.Clave);

                if (claveBD != claveIngresadaHash)
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }

                HttpContext.Session.SetString("IdUsuario", idUsuario);
                HttpContext.Session.SetString("NombreUsuario", nombreUsuario);
                HttpContext.Session.SetString("IdRol", idRol);
                HttpContext.Session.SetString("NombreRol", nombreRol);

                lector.Close();

                string rolNormalizado = nombreRol.Trim().ToLower();

                // ===============================
                // RECORDAR SESION SOLO PARA CLIENTES
                // ===============================

                if (rolNormalizado == "cliente")
                {
                    if (modelo.Recordarme)
                    {
                        string token = GenerarTokenSeguro();
                        DateTime fechaExpiracion = DateTime.Now.AddDays(30);

                        string actualizarToken = @"
                            UPDATE usuario
                            SET token_recordarme = @token,
                                fecha_expiracion_recordarme = @fecha_expiracion
                            WHERE id_usuario = @id_usuario;";

                        using var comandoActualizar = new MySqlCommand(actualizarToken, conexion);
                        comandoActualizar.Parameters.AddWithValue("@token", token);
                        comandoActualizar.Parameters.AddWithValue("@fecha_expiracion", fechaExpiracion);
                        comandoActualizar.Parameters.AddWithValue("@id_usuario", idUsuario);

                        comandoActualizar.ExecuteNonQuery();

                        CookieOptions opcionesCookie = new CookieOptions
                        {
                            Expires = fechaExpiracion,
                            HttpOnly = true,
                            IsEssential = true,
                            Secure = false
                        };

                        Response.Cookies.Append("MiHotel_Recordarme", token, opcionesCookie);
                    }
                    else
                    {
                        string limpiarToken = @"
                            UPDATE usuario
                            SET token_recordarme = NULL,
                                fecha_expiracion_recordarme = NULL
                            WHERE id_usuario = @id_usuario;";

                        using var comandoLimpiar = new MySqlCommand(limpiarToken, conexion);
                        comandoLimpiar.Parameters.AddWithValue("@id_usuario", idUsuario);
                        comandoLimpiar.ExecuteNonQuery();

                        Response.Cookies.Delete("MiHotel_Recordarme");
                    }
                }
                else
                {
                    Response.Cookies.Delete("MiHotel_Recordarme");
                }

                return RedirectToAction("Index", "Panel");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al iniciar sesión: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // REGISTRO DE CLIENTE - VISTA
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Registro()
        {
            if (HttpContext.Session.GetString("IdUsuario") != null)
            {
                return RedirectToAction("Index", "Panel");
            }

            CargarDatosConfiguracion();
            return View();
        }

        // ===============================
        // REGISTRO DE CLIENTE - GUARDAR
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Registro(RegistroCliente modelo)
        {
            CargarDatosConfiguracion();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
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
                    return View(modelo);
                }

                string obtenerRol = @"
                    SELECT id_rol
                    FROM rol
                    WHERE nombre_rol = 'cliente'
                    LIMIT 1;";

                using var comandoRol = new MySqlCommand(obtenerRol, conexion);
                object? resultadoRol = comandoRol.ExecuteScalar();

                if (resultadoRol == null)
                {
                    ViewBag.Mensaje = "No se encontró el rol de cliente en la base de datos.";
                    return View(modelo);
                }

                int idRolCliente = Convert.ToInt32(resultadoRol);
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
                        'activo'
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_rol", idRolCliente);
                comandoInsertar.Parameters.AddWithValue("@nombre", modelo.Nombre);
                comandoInsertar.Parameters.AddWithValue("@correo", modelo.Correo);
                comandoInsertar.Parameters.AddWithValue("@telefono", modelo.Telefono);
                comandoInsertar.Parameters.AddWithValue("@clave", claveHash);

                comandoInsertar.ExecuteNonQuery();

                ViewBag.Exito = "Cuenta creada correctamente. Ahora puede iniciar sesión.";
                ModelState.Clear();

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al registrar cliente: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // CIERRE DE SESION
        // ===============================

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CerrarSesion()
        {
            try
            {
                string? idUsuario = HttpContext.Session.GetString("IdUsuario");
                string? nombreRol = HttpContext.Session.GetString("NombreRol");

                if (!string.IsNullOrEmpty(idUsuario) && !string.IsNullOrEmpty(nombreRol))
                {
                    if (nombreRol.Trim().ToLower() == "cliente")
                    {
                        using var conexion = _conexionBD.ObtenerConexion();
                        conexion.Open();

                        string limpiarToken = @"
                    UPDATE usuario
                    SET token_recordarme = NULL,
                        fecha_expiracion_recordarme = NULL
                    WHERE id_usuario = @id_usuario;";

                        using var comandoLimpiar = new MySqlCommand(limpiarToken, conexion);
                        comandoLimpiar.Parameters.AddWithValue("@id_usuario", idUsuario);
                        comandoLimpiar.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                // Si ocurre un error al limpiar el recordatorio,
                // igual se continúa con el cierre de sesión.
            }

            HttpContext.Session.Clear();
            Response.Cookies.Delete("MiHotel_Recordarme");

            return RedirectToAction("Login", "Acceso");
        }
    }
}