// ===============================
// VIEWMODEL DE RESULTADO DE DISPONIBILIDAD
// ===============================

namespace MiHotel.Models
{
    public class DisponibilidadResultadoViewModel
    {
        // ===============================
        // ID DE LA HABITACION
        // ===============================
        public int IdHabitacion { get; set; }

        // ===============================
        // NUMERO DE HABITACION
        // ===============================
        public string NumeroHabitacion { get; set; } = string.Empty;

        // ===============================
        // TIPO DE HABITACION
        // ===============================
        public string TipoHabitacion { get; set; } = string.Empty;

        // ===============================
        // PRECIO POR NOCHE
        // ===============================
        public decimal Precio { get; set; }

        // ===============================
        // ESTADO FISICO
        // ===============================
        public string Estado { get; set; } = string.Empty;
    }
}