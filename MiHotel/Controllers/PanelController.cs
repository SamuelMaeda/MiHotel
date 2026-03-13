using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace MiHotel.Controllers
{
    public class PanelController : Controller
    {
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");

            if (string.IsNullOrEmpty(idUsuario))
            {
                return RedirectToAction("Login", "Acceso");
            }

            ViewBag.NombreUsuario = HttpContext.Session.GetString("NombreUsuario");
            ViewBag.NombreRol = HttpContext.Session.GetString("NombreRol");

            return View();
        }
    }
}