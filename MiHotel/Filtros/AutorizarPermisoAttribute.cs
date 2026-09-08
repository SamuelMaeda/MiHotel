using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MiHotel.Services;

namespace MiHotel.Filtros;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class AutorizarPermisoAttribute : ActionFilterAttribute
{
    private readonly string _permiso;

    public AutorizarPermisoAttribute(string permiso)
    {
        _permiso = permiso;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrWhiteSpace(context.HttpContext.Session.GetString("IdUsuario")))
        {
            context.Result = new RedirectToActionResult("Login", "Acceso", null);
            return;
        }

        var permisos = context.HttpContext.RequestServices
            .GetRequiredService<PermisosUsuarioService>();

        if (!permisos.TienePermiso(_permiso))
        {
            if (context.Controller is Controller controller)
                controller.TempData["Mensaje"] = "No tiene el permiso necesario para realizar esta acción.";

            context.Result = new RedirectToActionResult("Index", "Panel", null);
        }
    }
}
