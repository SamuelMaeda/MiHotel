using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MiHotel.Filtros
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AutorizarRolesAttribute : ActionFilterAttribute
    {
        private readonly HashSet<string> _rolesPermitidos;

        public AutorizarRolesAttribute(params string[] rolesPermitidos)
        {
            _rolesPermitidos = rolesPermitidos
                .Select(rol => rol.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            string? idUsuario = context.HttpContext.Session.GetString("IdUsuario");
            if (string.IsNullOrWhiteSpace(idUsuario))
            {
                context.Result = new RedirectToActionResult("Login", "Acceso", null);
                return;
            }

            string rol = context.HttpContext.Session
                .GetString("NombreRol")?
                .Trim()
                .ToLowerInvariant() ?? "";

            if (!_rolesPermitidos.Contains(rol))
            {
                if (context.Controller is Controller controller)
                {
                    controller.TempData["Mensaje"] = "No tiene permisos para realizar esta acción.";
                }
                context.Result = new RedirectToActionResult("Index", "Panel", null);
            }
        }
    }
}
