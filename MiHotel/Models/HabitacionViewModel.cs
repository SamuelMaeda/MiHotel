// Este modelo sirve para el listado.
namespace MiHotel.Models
{
    public class HabitacionViewModel
    {
        public int IdHabitacion { get; set; }
        public int IdProser { get; set; }

        public string NumeroHabitacion { get; set; } = string.Empty;
        public string TipoHabitacion { get; set; } = string.Empty;
        public int Capacidad { get; set; }
        public decimal Precio { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Piso { get; set; }
        public string? Descripcion { get; set; }
    }
}