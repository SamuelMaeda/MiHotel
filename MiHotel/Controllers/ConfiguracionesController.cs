using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class ConfiguracionesController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public ConfiguracionesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarAccesoAdmin()
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("IdUsuario")))
            {
                return RedirectToAction("Login", "Acceso");
            }

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";

            if (rol != "admin")
            {
                TempData["Mensaje"] = "Solo los administradores pueden modificar las configuraciones.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index()
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var comando = new MySqlCommand(@"
                    SELECT recargo_tarjeta, fecha_actualizacion
                    FROM configuracion_sistema
                    WHERE id_configuracion = 1
                    LIMIT 1;", conexion);

                using var lector = comando.ExecuteReader();
                var modelo = new ConfiguracionRecargoViewModel();

                if (lector.Read())
                {
                    modelo.RecargoTarjeta = Convert.ToDecimal(lector["recargo_tarjeta"]);
                    modelo.FechaActualizacion = lector["fecha_actualizacion"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(lector["fecha_actualizacion"]);
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No se pudo cargar la configuración: " + ex.Message;
                return View(new ConfiguracionRecargoViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(ConfiguracionRecargoViewModel modelo)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null) return acceso;

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var comando = new MySqlCommand(@"
                    INSERT INTO configuracion_sistema
                        (id_configuracion, recargo_tarjeta, fecha_actualizacion, id_usuario_actualizacion)
                    VALUES
                        (1, @recargo_tarjeta, NOW(), @id_usuario)
                    ON DUPLICATE KEY UPDATE
                        recargo_tarjeta = VALUES(recargo_tarjeta),
                        fecha_actualizacion = NOW(),
                        id_usuario_actualizacion = VALUES(id_usuario_actualizacion);", conexion);

                comando.Parameters.AddWithValue("@recargo_tarjeta", modelo.RecargoTarjeta);
                comando.Parameters.AddWithValue(
                    "@id_usuario",
                    Convert.ToInt32(HttpContext.Session.GetString("IdUsuario")));
                comando.ExecuteNonQuery();

                TempData["Exito"] = "El recargo por pago con tarjeta se actualizó correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No se pudo guardar la configuración: " + ex.Message;
                return View(modelo);
            }
        }
    }
}
