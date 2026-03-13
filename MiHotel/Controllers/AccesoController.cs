using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Utilidades;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class AccesoController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public AccesoController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("IdUsuario") != null)
            {
                return RedirectToAction("Index", "Panel");
            }

            return View();
        }

        [HttpPost]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login(InicioSesion modelo)
        {
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

                if (lector.Read())
                {
                    string claveBD = lector["clave"].ToString() ?? "";
                    string estado = lector["estado"].ToString() ?? "";
                    string nombreUsuario = lector["nombre_usuario"].ToString() ?? "";
                    string idUsuario = lector["id_usuario"].ToString() ?? "";
                    string idRol = lector["id_rol"].ToString() ?? "";
                    string nombreRol = lector["nombre_rol"].ToString() ?? "";

                    if (estado.ToLower() != "activo")
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

                    return RedirectToAction("Index", "Panel");
                }
                else
                {
                    ViewBag.Mensaje = "Correo o clave incorrectos.";
                    return View(modelo);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al iniciar sesión: " + ex.Message;
                return View(modelo);
            }
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CerrarSesion()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Acceso");
        }
    }
}