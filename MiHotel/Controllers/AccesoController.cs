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
            ViewBag.EmpresaNombre = _configSistema.Empresa?.Nombre ?? "MiHotel";
            ViewBag.EmpresaLogo = _configSistema.Empresa?.Logo ?? "logo.png";
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
            return (telefono ?? "").Replace(" ", "").Trim();
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
        // OBTENER ID DE TIPO CLIENTE
        // ===============================

        private int ObtenerIdTipoCliente(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_tipoclipro
                FROM tipo_clipro
                WHERE LOWER(tipo) = 'cliente'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe el tipo 'cliente' en tipo_clipro.");
            }

            return Convert.ToInt32(resultado);
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

            string asunto = "Recuperación de contraseña - " + (_configSistema.Empresa?.Nombre ?? "MiHotel");

            string contenidoHtml = $@"
                <html>
                <body style='margin:0; padding:0; background-color:#f5f1f0; font-family:Arial, sans-serif; color:#363636;'>
                    <div style='max-width:600px; margin:30px auto; background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.12);'>
                        
                        <div style='background-color:#824B44; padding:25px; text-align:center;'>
                            <img src='cid:logoHotel' alt='{_configSistema.Empresa?.Nombre ?? "MiHotel"}' style='max-height:90px; width:auto; margin-bottom:10px;' />
                            <h2 style='margin:0; color:#ffffff;'>Recuperación de contraseña</h2>
                        </div>

                        <div style='padding:30px;'>
                            <p style='margin-top:0;'>Hola <strong>{nombreUsuario}</strong>,</p>

                            <p>
                                Hemos recibido una solicitud para restablecer tu contraseña en el sistema de
                                <strong>{_configSistema.Empresa?.Nombre ?? "MiHotel"}</strong>.
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

            string nombreMostrar = string.IsNullOrWhiteSpace(_configSistema.Correo?.NombreMostrar)
                ? (_configSistema.Empresa?.Nombre ?? "MiHotel") + " - No Reply"
                : _configSistema.Correo.NombreMostrar;

            mensaje.From = new MailAddress(_configSistema.Correo!.Remitente, nombreMostrar);
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
            // VALIDAR COOKIE DE RECORDAR SESION PARA CLIENTES
            // ===============================

            if (Request.Cookies.TryGetValue("MiHotel_Recordarme", out string? tokenCookie))
            {
                try
                {
                    using var conexion = _conexionBD.ObtenerConexion();
                    conexion.Open();

                    int idTipoCliente = ObtenerIdTipoCliente(conexion);

                    string consultaToken = @"
                        SELECT 
                            c.id_clipro,
                            c.nombre,
                            c.estado,
                            c.fecha_expiracion_recordarme
                        FROM clipro c
                        WHERE c.id_tipoclipro = @id_tipoclipro
                          AND c.token_recordarme = @token
                        LIMIT 1;";

                    using var comandoToken = new MySqlCommand(consultaToken, conexion);
                    comandoToken.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                    comandoToken.Parameters.AddWithValue("@token", tokenCookie);

                    using var lector = comandoToken.ExecuteReader();

                    if (lector.Read())
                    {
                        string idClipro = lector["id_clipro"]?.ToString() ?? "";
                        string nombreCliente = lector["nombre"]?.ToString() ?? "";
                        string estado = lector["estado"]?.ToString() ?? "";

                        DateTime fechaExpiracion = Convert.ToDateTime(lector["fecha_expiracion_recordarme"]);

                        bool clienteActivo = estado.Trim().ToLower() == "activo";
                        bool tokenVigente = fechaExpiracion >= DateTime.Now;

                        if (clienteActivo && tokenVigente)
                        {
                            HttpContext.Session.SetString("IdUsuario", idClipro);
                            HttpContext.Session.SetString("NombreUsuario", nombreCliente);
                            HttpContext.Session.SetString("IdRol", "cliente");
                            HttpContext.Session.SetString("NombreRol", "cliente");

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

                string correoIngresado = modelo.Correo.Trim();
                string claveIngresadaHash = SeguridadHelper.ObtenerSha256(modelo.Clave);

                // ===============================
                // INTENTAR LOGIN COMO USUARIO ADMINISTRATIVO
                // ===============================

                string consultaUsuario = @"
                    SELECT 
                        u.id_usuario,
                        u.nombre_usuario,
                        u.clave,
                        u.estado,
                        u.id_rol,
                        r.nombre_rol
                    FROM usuario u
                    INNER JOIN rol r ON u.id_rol = r.id_rol
                    WHERE u.correo = @correo
                    LIMIT 1;";

                using (var comandoUsuario = new MySqlCommand(consultaUsuario, conexion))
                {
                    comandoUsuario.Parameters.AddWithValue("@correo", correoIngresado);

                    using var lectorUsuario = comandoUsuario.ExecuteReader();

                    if (lectorUsuario.Read())
                    {
                        string claveBD = lectorUsuario["clave"]?.ToString() ?? "";
                        string estado = lectorUsuario["estado"]?.ToString() ?? "";
                        string nombreUsuario = lectorUsuario["nombre_usuario"]?.ToString() ?? "";
                        string idUsuario = lectorUsuario["id_usuario"]?.ToString() ?? "";
                        string idRol = lectorUsuario["id_rol"]?.ToString() ?? "";
                        string nombreRol = lectorUsuario["nombre_rol"]?.ToString() ?? "";

                        if (estado.Trim().ToLower() != "activo")
                        {
                            ViewBag.Mensaje = "El usuario está inactivo.";
                            return View(modelo);
                        }

                        if (claveBD != claveIngresadaHash)
                        {
                            ViewBag.Mensaje = "Correo o clave incorrectos.";
                            return View(modelo);
                        }

                        HttpContext.Session.SetString("IdUsuario", idUsuario);
                        HttpContext.Session.SetString("NombreUsuario", nombreUsuario);
                        HttpContext.Session.SetString("IdRol", idRol);
                        HttpContext.Session.SetString("NombreRol", nombreRol);

                        return RedirectToAction("Index", "Panel");
                    }
                }

                // ===============================
                // INTENTAR LOGIN COMO CLIENTE EN CLIPRO
                // ===============================

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string consultaCliente = @"
                    SELECT
                        c.id_clipro,
                        c.nombre,
                        c.correo,
                        c.clave,
                        c.estado
                    FROM clipro c
                    WHERE c.id_tipoclipro = @id_tipoclipro
                      AND c.correo = @correo
                    LIMIT 1;";

                using var comandoCliente = new MySqlCommand(consultaCliente, conexion);
                comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                comandoCliente.Parameters.AddWithValue("@correo", correoIngresado);

                using var lectorCliente = comandoCliente.ExecuteReader();

                if (!lectorCliente.Read())
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }

                string claveClienteBD = lectorCliente["clave"]?.ToString() ?? "";
                string estadoCliente = lectorCliente["estado"]?.ToString() ?? "";
                string nombreCliente = lectorCliente["nombre"]?.ToString() ?? "";
                string idClipro = lectorCliente["id_clipro"]?.ToString() ?? "";

                if (estadoCliente.Trim().ToLower() != "activo")
                {
                    ViewBag.Mensaje = "El cliente está inactivo.";
                    return View(modelo);
                }

                if (string.IsNullOrWhiteSpace(claveClienteBD))
                {
                    ViewBag.Mensaje = "La cuenta del cliente no tiene acceso habilitado.";
                    return View(modelo);
                }

                if (claveClienteBD != claveIngresadaHash)
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }

                HttpContext.Session.SetString("IdUsuario", idClipro);
                HttpContext.Session.SetString("NombreUsuario", nombreCliente);
                HttpContext.Session.SetString("IdRol", "cliente");
                HttpContext.Session.SetString("NombreRol", "cliente");

                lectorCliente.Close();

                // ===============================
                // RECORDAR SESION PARA CLIENTES
                // ===============================

                if (modelo.Recordarme)
                {
                    string token = GenerarTokenSeguro();
                    DateTime fechaExpiracion = DateTime.Now.AddDays(30);

                    string actualizarToken = @"
                        UPDATE clipro
                        SET token_recordarme = @token,
                            fecha_expiracion_recordarme = @fecha_expiracion
                        WHERE id_clipro = @id_clipro
                          AND id_tipoclipro = @id_tipoclipro;";

                    using var comandoActualizar = new MySqlCommand(actualizarToken, conexion);
                    comandoActualizar.Parameters.AddWithValue("@token", token);
                    comandoActualizar.Parameters.AddWithValue("@fecha_expiracion", fechaExpiracion);
                    comandoActualizar.Parameters.AddWithValue("@id_clipro", idClipro);
                    comandoActualizar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

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
                        UPDATE clipro
                        SET token_recordarme = NULL,
                            fecha_expiracion_recordarme = NULL
                        WHERE id_clipro = @id_clipro
                          AND id_tipoclipro = @id_tipoclipro;";

                    using var comandoLimpiar = new MySqlCommand(limpiarToken, conexion);
                    comandoLimpiar.Parameters.AddWithValue("@id_clipro", idClipro);
                    comandoLimpiar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                    comandoLimpiar.ExecuteNonQuery();

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
                modelo.Telefono = NormalizarTelefono(modelo.Telefono);

                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string correoNormalizado = string.IsNullOrWhiteSpace(modelo.Correo)
                    ? null!
                    : modelo.Correo.Trim();

                if (!string.IsNullOrWhiteSpace(correoNormalizado))
                {
                    string verificarCorreoClipro = @"
                        SELECT COUNT(*)
                        FROM clipro
                        WHERE id_tipoclipro = @id_tipoclipro
                          AND correo = @correo;";

                    using var comandoVerificarClipro = new MySqlCommand(verificarCorreoClipro, conexion);
                    comandoVerificarClipro.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                    comandoVerificarClipro.Parameters.AddWithValue("@correo", correoNormalizado);

                    int existeClipro = Convert.ToInt32(comandoVerificarClipro.ExecuteScalar());

                    if (existeClipro > 0)
                    {
                        ViewBag.Mensaje = "El correo ya está registrado.";
                        modelo.Telefono = FormatearTelefono(modelo.Telefono);
                        return View(modelo);
                    }

                    string verificarCorreoUsuario = @"
                        SELECT COUNT(*)
                        FROM usuario
                        WHERE correo = @correo;";

                    using var comandoVerificarUsuario = new MySqlCommand(verificarCorreoUsuario, conexion);
                    comandoVerificarUsuario.Parameters.AddWithValue("@correo", correoNormalizado);

                    int existeUsuario = Convert.ToInt32(comandoVerificarUsuario.ExecuteScalar());

                    if (existeUsuario > 0)
                    {
                        ViewBag.Mensaje = "El correo ya está registrado.";
                        modelo.Telefono = FormatearTelefono(modelo.Telefono);
                        return View(modelo);
                    }
                }

                string claveHash = SeguridadHelper.ObtenerSha256(modelo.Clave);

                string insertar = @"
                    INSERT INTO clipro
                    (
                        id_tipoclipro,
                        nombre,
                        nit,
                        direccion,
                        nombre_empresa,
                        numero_empresa,
                        telefono,
                        correo,
                        clave,
                        token_recordarme,
                        fecha_expiracion_recordarme,
                        token_recuperacion,
                        fecha_expiracion_recuperacion,
                        estado
                    )
                    VALUES
                    (
                        @id_tipoclipro,
                        @nombre,
                        NULL,
                        NULL,
                        NULL,
                        NULL,
                        @telefono,
                        @correo,
                        @clave,
                        NULL,
                        NULL,
                        NULL,
                        NULL,
                        'activo'
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                comandoInsertar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comandoInsertar.Parameters.AddWithValue("@telefono", modelo.Telefono);
                comandoInsertar.Parameters.AddWithValue("@correo", correoNormalizado);
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

                string correoIngresado = modelo.Correo.Trim();

                // ===============================
                // BUSCAR EN USUARIO
                // ===============================

                string consultaUsuario = @"
                    SELECT id_usuario, nombre_usuario, correo
                    FROM usuario
                    WHERE correo = @correo
                      AND estado = 'activo'
                    LIMIT 1;";

                using (var comandoUsuario = new MySqlCommand(consultaUsuario, conexion))
                {
                    comandoUsuario.Parameters.AddWithValue("@correo", correoIngresado);

                    using var lectorUsuario = comandoUsuario.ExecuteReader();

                    if (lectorUsuario.Read())
                    {
                        string idUsuario = lectorUsuario["id_usuario"]?.ToString() ?? "";
                        string nombreUsuario = lectorUsuario["nombre_usuario"]?.ToString() ?? "";
                        string correo = lectorUsuario["correo"]?.ToString() ?? "";

                        lectorUsuario.Close();

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
                }

                // ===============================
                // BUSCAR EN CLIPRO COMO CLIENTE
                // ===============================

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string consultaCliente = @"
                    SELECT id_clipro, nombre, correo
                    FROM clipro
                    WHERE id_tipoclipro = @id_tipoclipro
                      AND correo = @correo
                      AND estado = 'activo'
                    LIMIT 1;";

                using var comandoCliente = new MySqlCommand(consultaCliente, conexion);
                comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                comandoCliente.Parameters.AddWithValue("@correo", correoIngresado);

                using var lectorCliente = comandoCliente.ExecuteReader();

                if (!lectorCliente.Read())
                {
                    ViewBag.Exito = "Si el correo existe en el sistema, se ha enviado un enlace de recuperación.";
                    ModelState.Clear();
                    return View();
                }

                string idClipro = lectorCliente["id_clipro"]?.ToString() ?? "";
                string nombreCliente = lectorCliente["nombre"]?.ToString() ?? "";
                string correoCliente = lectorCliente["correo"]?.ToString() ?? "";

                lectorCliente.Close();

                string tokenCliente = GenerarTokenRecuperacion();
                DateTime fechaExpiracionCliente = DateTime.Now.AddMinutes(30);

                string actualizarCliente = @"
                    UPDATE clipro
                    SET token_recuperacion = @token,
                        fecha_expiracion_recuperacion = @fecha_expiracion
                    WHERE id_clipro = @id_clipro
                      AND id_tipoclipro = @id_tipoclipro;";

                using var comandoActualizarCliente = new MySqlCommand(actualizarCliente, conexion);
                comandoActualizarCliente.Parameters.AddWithValue("@token", tokenCliente);
                comandoActualizarCliente.Parameters.AddWithValue("@fecha_expiracion", fechaExpiracionCliente);
                comandoActualizarCliente.Parameters.AddWithValue("@id_clipro", idClipro);
                comandoActualizarCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                comandoActualizarCliente.ExecuteNonQuery();

                EnviarCorreoRecuperacion(correoCliente, nombreCliente, tokenCliente);

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

                string consultaUsuario = @"
                    SELECT COUNT(*)
                    FROM usuario
                    WHERE token_recuperacion = @token
                      AND fecha_expiracion_recuperacion >= @fecha_actual
                      AND estado = 'activo';";

                using var comandoUsuario = new MySqlCommand(consultaUsuario, conexion);
                comandoUsuario.Parameters.AddWithValue("@token", token);
                comandoUsuario.Parameters.AddWithValue("@fecha_actual", DateTime.Now);

                int existeUsuario = Convert.ToInt32(comandoUsuario.ExecuteScalar());

                if (existeUsuario > 0)
                {
                    RestablecerClave modeloUsuario = new RestablecerClave
                    {
                        Token = token
                    };

                    return View(modeloUsuario);
                }

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string consultaCliente = @"
                    SELECT COUNT(*)
                    FROM clipro
                    WHERE id_tipoclipro = @id_tipoclipro
                      AND token_recuperacion = @token
                      AND fecha_expiracion_recuperacion >= @fecha_actual
                      AND estado = 'activo';";

                using var comandoCliente = new MySqlCommand(consultaCliente, conexion);
                comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                comandoCliente.Parameters.AddWithValue("@token", token);
                comandoCliente.Parameters.AddWithValue("@fecha_actual", DateTime.Now);

                int existeCliente = Convert.ToInt32(comandoCliente.ExecuteScalar());

                if (existeCliente == 0)
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

                string nuevaClaveHash = SeguridadHelper.ObtenerSha256(modelo.NuevaClave);

                // ===============================
                // INTENTAR ACTUALIZAR EN USUARIO
                // ===============================

                string consultaUsuario = @"
                    SELECT id_usuario
                    FROM usuario
                    WHERE token_recuperacion = @token
                      AND fecha_expiracion_recuperacion >= @fecha_actual
                      AND estado = 'activo'
                    LIMIT 1;";

                using (var comandoUsuario = new MySqlCommand(consultaUsuario, conexion))
                {
                    comandoUsuario.Parameters.AddWithValue("@token", modelo.Token);
                    comandoUsuario.Parameters.AddWithValue("@fecha_actual", DateTime.Now);

                    object? resultadoUsuario = comandoUsuario.ExecuteScalar();

                    if (resultadoUsuario != null)
                    {
                        int idUsuario = Convert.ToInt32(resultadoUsuario);

                        string actualizarUsuario = @"
                            UPDATE usuario
                            SET clave = @clave,
                                token_recuperacion = NULL,
                                fecha_expiracion_recuperacion = NULL,
                                token_recordarme = NULL,
                                fecha_expiracion_recordarme = NULL
                            WHERE id_usuario = @id_usuario;";

                        using var comandoActualizarUsuario = new MySqlCommand(actualizarUsuario, conexion);
                        comandoActualizarUsuario.Parameters.AddWithValue("@clave", nuevaClaveHash);
                        comandoActualizarUsuario.Parameters.AddWithValue("@id_usuario", idUsuario);

                        comandoActualizarUsuario.ExecuteNonQuery();

                        TempData["Exito"] = "La contraseña se restableció correctamente. Ahora puede iniciar sesión.";
                        return RedirectToAction("Login");
                    }
                }

                // ===============================
                // INTENTAR ACTUALIZAR EN CLIPRO
                // ===============================

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string consultaCliente = @"
                    SELECT id_clipro
                    FROM clipro
                    WHERE id_tipoclipro = @id_tipoclipro
                      AND token_recuperacion = @token
                      AND fecha_expiracion_recuperacion >= @fecha_actual
                      AND estado = 'activo'
                    LIMIT 1;";

                using var comandoCliente = new MySqlCommand(consultaCliente, conexion);
                comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                comandoCliente.Parameters.AddWithValue("@token", modelo.Token);
                comandoCliente.Parameters.AddWithValue("@fecha_actual", DateTime.Now);

                object? resultadoCliente = comandoCliente.ExecuteScalar();

                if (resultadoCliente == null)
                {
                    ViewBag.Mensaje = "El enlace de recuperación no es válido o ha expirado.";
                    return View(modelo);
                }

                int idClipro = Convert.ToInt32(resultadoCliente);

                string actualizarCliente = @"
                    UPDATE clipro
                    SET clave = @clave,
                        token_recuperacion = NULL,
                        fecha_expiracion_recuperacion = NULL,
                        token_recordarme = NULL,
                        fecha_expiracion_recordarme = NULL
                    WHERE id_clipro = @id_clipro
                      AND id_tipoclipro = @id_tipoclipro;";

                using var comandoActualizarCliente = new MySqlCommand(actualizarCliente, conexion);
                comandoActualizarCliente.Parameters.AddWithValue("@clave", nuevaClaveHash);
                comandoActualizarCliente.Parameters.AddWithValue("@id_clipro", idClipro);
                comandoActualizarCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                comandoActualizarCliente.ExecuteNonQuery();

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

                        int idTipoCliente = ObtenerIdTipoCliente(conexion);

                        string limpiarToken = @"
                            UPDATE clipro
                            SET token_recordarme = NULL,
                                fecha_expiracion_recordarme = NULL
                            WHERE id_clipro = @id_clipro
                              AND id_tipoclipro = @id_tipoclipro;";

                        using var comandoLimpiar = new MySqlCommand(limpiarToken, conexion);
                        comandoLimpiar.Parameters.AddWithValue("@id_clipro", idUsuario);
                        comandoLimpiar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
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