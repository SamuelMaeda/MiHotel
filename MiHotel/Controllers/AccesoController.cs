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
                    WHERE u.correo = @correo
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@correo", modelo.Correo.Trim());

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }

                string claveBd = lector["clave"]?.ToString() ?? "";
                string estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";
                string claveIngresadaHash = SeguridadHelper.ObtenerSha256(modelo.Clave);

                if (estado != "activo")
                {
                    ViewBag.Mensaje = "El usuario está inactivo.";
                    return View(modelo);
                }

                if (claveBd != claveIngresadaHash)
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }

                HttpContext.Session.SetString("IdUsuario", lector["id_usuario"]?.ToString() ?? "");
                HttpContext.Session.SetString("NombreUsuario", lector["nombre_usuario"]?.ToString() ?? "");
                HttpContext.Session.SetString("IdRol", lector["id_rol"]?.ToString() ?? "");
                HttpContext.Session.SetString("NombreRol", lector["nombre_rol"]?.ToString() ?? "");

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
