using Microsoft.AspNetCore.Mvc;

namespace MiHotel.Controllers
{
    public class PanelController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}