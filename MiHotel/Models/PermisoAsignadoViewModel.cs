namespace MiHotel.Models
{
    public class PermisoAsignadoViewModel
    {
        public int IdPermiso { get; set; }

        public string NombrePermiso { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public bool Asignado { get; set; }
    }
}