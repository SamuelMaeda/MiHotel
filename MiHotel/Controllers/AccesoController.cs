// ===============================
// CONTROLADOR DE ACCESO
// ===============================

using System.Net;
using System.Net.Mail;
using System.Net.Mime;
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
        // GENERAR TOKEN SEGURO PARA RECORDARME
        // ===============================

        private string GenerarTokenSeguro()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        // ===============================
        // GENERAR TOKEN DE RECUPERACION
        // ===============================

        private string GenerarTokenRecuperacion()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(bytes);
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
        // ENVIAR CORREO DE RECUPERACION
        // ===============================

        private void EnviarCorreoRecuperacion(string correoDestino, string nombreUsuario, string token)
        {
            string enlaceRecuperacion = Url.Action(
                action: "RestablecerClave",
                controller: "Acceso",
                values: new { token = token },
                protocol: Request.Scheme
            ) ?? string.Empty;

            string rutaLogo = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "logo.png"
            );

            string asunto = "Recuperación de contraseña - " + _configSistema.Empresa.Nombre;

            string contenidoHtml = $@"
                <html>
                <body style='margin:0; padding:0; background-color:#f5f1f0; font-family:Arial, sans-serif; color:#363636;'>
                    <div style='max-width:600px; margin:30px auto; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.12);'>
                        
                        <div style='background-color:#824B44; padding:25px; text-align:center;'>
                            <img src='cid:logoHotel' alt='{_configSistema.Empresa.Nombre}' style='max-height:90px; width:auto; margin-bottom:10px;' />
                            <h2 style='margin:0; color:#ffffff;'>Recuperación de contraseña</h2>
                        </div>

                        <div style='padding:30px;'>
                            <p style='margin-top:0;'>Hola <strong>{nombreUsuario}</strong>,</p>

                            <p>
                                Hemos recibido una solicitud para restablecer tu contraseña en el sistema de
                                <strong>{_configSistema.Empresa.Nombre}</strong>.
                            </p>

                            <p>
                                Para continuar, haz clic en el siguiente botón:
                            </p>

                            <div style='text-align:center; margin:30px 0;'>
                                <a href='{enlaceRecuperacion}'
                                   style='background-color:#464482; color:#ffffff; text-decoration:none; padding:14px 22px; border-radius:8px; display:inline-block; font-weight:bold;'>
                                    Restablecer contraseña
                                </a>
                            </div>

                            <p>
                                Este enlace expirará en <strong>30 minutos</strong>.
                            </p>

                            <p>
                                Si no solicitaste este cambio, puedes ignorar este mensaje de forma segura.
                            </p>

                            <hr style='border:none; border-top:1px solid #e1d6d3; margin:25px 0;' />

                            <p style='font-size:12px; color:#777777; margin-bottom:0;'>
                                Este es un correo automático del sistema. Por favor no responda a este mensaje.
                            </p>
                        </div>

                    </div>
                </body>
                </html>";

            using var mensaje = new MailMessage();

            string nombreMostrar = string.IsNullOrWhiteSpace(_configSistema.Correo.NombreMostrar)
                ? _configSistema.Empresa.Nombre + " - No Reply"
                : _configSistema.Correo.NombreMostrar;

            mensaje.From = new MailAddress(_configSistema.Correo.Remitente, nombreMostrar);
            mensaje.To.Add(correoDestino);

            // ===============================
            // REPLY-TO Y CABECERAS DE CORREO AUTOMATICO
            // ===============================

            mensaje.ReplyToList.Clear();
            mensaje.ReplyToList.Add(new MailAddress(_configSistema.Correo.Remitente, nombreMostrar));

            mensaje.Headers.Add("Auto-Submitted", "auto-generated");
            mensaje.Headers.Add("X-Auto-Response-Suppress", "All");
            mensaje.Headers.Add("Precedence", "bulk");

            mensaje.Subject = asunto;
            mensaje.SubjectEncoding = System.Text.Encoding.UTF8;
            mensaje.BodyEncoding = System.Text.Encoding.UTF8;
            mensaje.IsBodyHtml = true;

            AlternateView vistaHtml = AlternateView.CreateAlternateViewFromString(
                contenidoHtml,
                System.Text.Encoding.UTF8,
                MediaTypeNames.Text.Html
            );

            // ===============================
            // EMBEBER LOGO EN EL CORREO
            // ===============================

            if (System.IO.File.Exists(rutaLogo))
            {
                LinkedResource logoRecurso = new LinkedResource(rutaLogo, MediaTypeNames.Image.Jpeg);
                logoRecurso.ContentId = "logoHotel";
                logoRecurso.TransferEncoding = TransferEncoding.Base64;
                vistaHtml.LinkedResources.Add(logoRecurso);
            }

            mensaje.AlternateViews.Add(vistaHtml);

            using var smtp = new SmtpClient(_configSistema.Correo.Servidor, _configSistema.Correo.Puerto);
            smtp.Credentials = new NetworkCredential(
                _configSistema.Correo.Remitente,
                _configSistema.Correo.ClaveAplicacion
            );
            smtp.EnableSsl = _configSistema.Correo.UsarSsl;

            smtp.Send(mensaje);
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

            if (TempData["Exito"] != null)
            {
                ViewBag.Exito = TempData["Exito"];
            }

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
                // ===============================
                // NORMALIZAR TELEFONO ANTES DE GUARDAR
                // ===============================

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
                    modelo.Telefono = FormatearTelefono(modelo.Telefono);
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
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                return View(modelo);
            }
        }

        // ===============================
        // VISTA SOLICITAR RECUPERACION
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult OlvideClave()
        {
            if (HttpContext.Session.GetString("IdUsuario") != null)
            {
                return RedirectToAction("Index", "Panel");
            }

            CargarDatosConfiguracion();
            return View();
        }

        // ===============================
        // ENVIAR ENLACE DE RECUPERACION
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult OlvideClave(SolicitarRecuperacion modelo)
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
                    SELECT id_usuario, nombre_usuario, correo
                    FROM usuario
                    WHERE correo = @correo
                      AND estado = 'activo'
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@correo", modelo.Correo);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    ViewBag.Exito = "Si el correo existe en el sistema, se ha enviado un enlace de recuperación.";
                    ModelState.Clear();
                    return View();
                }

                string idUsuario = lector["id_usuario"]?.ToString() ?? "";
                string nombreUsuario = lector["nombre_usuario"]?.ToString() ?? "";
                string correo = lector["correo"]?.ToString() ?? "";

                lector.Close();

                string token = GenerarTokenRecuperacion();
                DateTime fechaExpiracion = DateTime.Now.AddMinutes(30);

                string actualizar = @"
                    UPDATE usuario
                    SET token_recuperacion = @token,
                        fecha_expiracion_recuperacion = @fecha_expiracion
                    WHERE id_usuario = @id_usuario;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@token", token);
                comandoActualizar.Parameters.AddWithValue("@fecha_expiracion", fechaExpiracion);
                comandoActualizar.Parameters.AddWithValue("@id_usuario", idUsuario);

                comandoActualizar.ExecuteNonQuery();

                EnviarCorreoRecuperacion(correo, nombreUsuario, token);

                ViewBag.Exito = "Si el correo existe en el sistema, se ha enviado un enlace de recuperación.";
                ModelState.Clear();

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al procesar la recuperación: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // VISTA RESTABLECER CLAVE
        // ===============================

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult RestablecerClave(string token)
        {
            CargarDatosConfiguracion();

            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Mensaje = "El enlace de recuperación no es válido.";
                return View(new RestablecerClave());
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT COUNT(*)
                    FROM usuario
                    WHERE token_recuperacion = @token
                      AND fecha_expiracion_recuperacion >= @fecha_actual
                      AND estado = 'activo';";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@token", token);
                comando.Parameters.AddWithValue("@fecha_actual", DateTime.Now);

                int existe = Convert.ToInt32(comando.ExecuteScalar());

                if (existe == 0)
                {
                    ViewBag.Mensaje = "El enlace de recuperación no es válido o ha expirado.";
                    return View(new RestablecerClave());
                }

                RestablecerClave modelo = new RestablecerClave
                {
                    Token = token
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al validar el enlace: " + ex.Message;
                return View(new RestablecerClave());
            }
        }

        // ===============================
        // GUARDAR NUEVA CLAVE RECUPERADA
        // ===============================

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult RestablecerClave(RestablecerClave modelo)
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
                    SELECT id_usuario
                    FROM usuario
                    WHERE token_recuperacion = @token
                      AND fecha_expiracion_recuperacion >= @fecha_actual
                      AND estado = 'activo'
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@token", modelo.Token);
                comando.Parameters.AddWithValue("@fecha_actual", DateTime.Now);

                object? resultado = comando.ExecuteScalar();

                if (resultado == null)
                {
                    ViewBag.Mensaje = "El enlace de recuperación no es válido o ha expirado.";
                    return View(modelo);
                }

                int idUsuario = Convert.ToInt32(resultado);
                string nuevaClaveHash = SeguridadHelper.ObtenerSha256(modelo.NuevaClave);

                string actualizar = @"
                    UPDATE usuario
                    SET clave = @clave,
                        token_recuperacion = NULL,
                        fecha_expiracion_recuperacion = NULL,
                        token_recordarme = NULL,
                        fecha_expiracion_recordarme = NULL
                    WHERE id_usuario = @id_usuario;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@clave", nuevaClaveHash);
                comandoActualizar.Parameters.AddWithValue("@id_usuario", idUsuario);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "La contraseña se restableció correctamente. Ahora puede iniciar sesión.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al restablecer la contraseña: " + ex.Message;
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