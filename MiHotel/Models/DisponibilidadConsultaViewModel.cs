// ===============================
// VIEWMODEL DE CONSULTA DE DISPONIBILIDAD
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class DisponibilidadConsultaViewModel
    {
        // ===============================
        // FECHA DE ENTRADA
        // ===============================
        [Required(ErrorMessage = "Ingrese la fecha de entrada.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de entrada")]
        public DateTime? FechaEntrada { get; set; }

        // ===============================
        // FECHA DE SALIDA
        // ===============================
        [Required(ErrorMessage = "Ingrese la fecha de salida.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de salida")]
        public DateTime? FechaSalida { get; set; }

        // ===============================
        // TIPO DE HABITACION OPCIONAL
        // ===============================
        [Display(Name = "Tipo de habitación")]
        public int? IdSubcategoria { get; set; }

        // ===============================
        // INDICA SI YA SE REALIZO LA CONSULTA
        // ===============================
        public bool Consultado { get; set; } = false;

        // ===============================
        // RESULTADOS DE LA CONSULTA
        // ===============================
        public List<DisponibilidadResultadoViewModel> HabitacionesDisponibles { get; set; } = new List<DisponibilidadResultadoViewModel>();
    }
}