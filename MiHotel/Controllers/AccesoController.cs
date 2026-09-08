using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Models.Configuracion;
using MiHotel.Services;
using MiHotel.Utilidades;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class AccesoController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly ConfigSistema _configSistema;
        private readonly SuspensionMonitorService _monitorSuspension;

        public AccesoController(
            ConexionBD conexionBD,
            IOptions<ConfigSistema> opcionesConfig,
            SuspensionMonitorService monitorSuspension)
        {
            _conexionBD = conexionBD;
            _configSistema = opcionesConfig.Value;
            _monitorSuspension = monitorSuspension;
        }

        private void CargarDatosConfiguracion()
        {
            ViewBag.EmpresaNombre = _configSistema.Empresa?.Nombre ?? "MiHotel";
            ViewBag.EmpresaLogo = _configSistema.Empresa?.Logo ?? "logo.png";
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("IdUsuario") != null)
            {
                return RedirectToAction("Index", "Panel");
            }

            CargarDatosConfiguracion();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

                const string consulta = @"
                    SELECT
                        u.id_usuario,
                        u.nombre_usuario,
                        u.clave,
                        u.estado,
                        u.id_rol,
                        r.nombre_rol
                    FROM usuario u
                    INNER JOIN rol r ON u.id_rol = r.id_rol
                    WHERE LOWER(u.correo) = LOWER(@identificador)
                       OR LOWER(u.nombre_usuario) = LOWER(@identificador)
                    ORDER BY CASE WHEN LOWER(u.correo) = LOWER(@identificador) THEN 0 ELSE 1 END
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@identificador", modelo.Identificador.Trim());

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    ViewBag.Mensaje = "Usuario, correo o clave incorrectos.";
                    return View(modelo);
                }

                int idUsuario = Convert.ToInt32(lector["id_usuario"]);
                string nombreUsuario = lector["nombre_usuario"]?.ToString() ?? "";
                int idRol = Convert.ToInt32(lector["id_rol"]);
                string nombreRol = lector["nombre_rol"]?.ToString() ?? "";
                string claveBd = lector["clave"]?.ToString() ?? "";
                string estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";
                lector.Close();

                if (estado != "activo")
                {
                    ViewBag.Mensaje = "El usuario está inactivo.";
                    return View(modelo);
                }

                if (!SeguridadHelper.VerificarClave(modelo.Clave, claveBd, out bool actualizarHash))
                {
                    ViewBag.Mensaje = "Usuario, correo o clave incorrectos.";
                    return View(modelo);
                }

                if (actualizarHash)
                {
                    using var actualizarClave = new MySqlCommand(
                        "UPDATE usuario SET clave=@clave WHERE id_usuario=@id;", conexion);
                    actualizarClave.Parameters.AddWithValue("@clave", SeguridadHelper.CrearHashClave(modelo.Clave));
                    actualizarClave.Parameters.AddWithValue("@id", idUsuario);
                    actualizarClave.ExecuteNonQuery();
                }

                HttpContext.Session.SetString("IdUsuario", idUsuario.ToString());
                HttpContext.Session.SetString("NombreUsuario", nombreUsuario);
                HttpContext.Session.SetString("IdRol", idRol.ToString());
                HttpContext.Session.SetString("NombreRol", nombreRol);
                HttpContext.Session.SetInt32("GeneracionSesion", _monitorSuspension.GeneracionActual);

                return RedirectToAction("Index", "Panel");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No se pudo iniciar sesión: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CerrarSesion()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Acceso");
        }
    }
}
