using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;

namespace MiHotel.Controllers
{
    public class PruebaController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public PruebaController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        public IActionResult Index()
        {
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                ViewBag.Mensaje = "Conexion exitosa a la base de datos Hotel.";
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error de conexion: " + ex.Message;
            }

            return View();
        }
    }
}