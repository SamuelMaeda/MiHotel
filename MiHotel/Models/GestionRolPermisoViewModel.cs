using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class GestionRolPermisoViewModel
    {
        public int IdRol { get; set; }

        [Display(Name = "Nombre del rol")]
        public string NombreRol { get; set; } = string.Empty;

        public List<PermisoAsignadoViewModel> Permisos { get; set; } = new List<PermisoAsignadoViewModel>();
    }
}
