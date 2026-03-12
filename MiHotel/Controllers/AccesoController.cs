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
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
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

                string consulta = @"SELECT id_usuario, nombre_usuario, correo, clave, estado, id_rol
                                    FROM usuario
                                    WHERE correo = @correo
                                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@correo", modelo.Correo);

                using var lector = comando.ExecuteReader();

                if (lector.Read())
                {
                    string claveBD = lector["clave"].ToString() ?? "";
                    string estado = lector["estado"].ToString() ?? "";

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
    }
}