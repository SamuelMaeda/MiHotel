// ===============================
// VIEWMODEL DE RESERVA
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ReservaFormViewModel
    {
        public int IdReserva { get; set; }

        [Required(ErrorMessage = "Seleccione un cliente.")]
        public int IdClipro { get; set; }

        [Required(ErrorMessage = "Seleccione una habitación.")]
        public int IdHabitacion { get; set; }

        [Required(ErrorMessage = "Ingrese la fecha de entrada.")]
        [DataType(DataType.Date)]
        public DateTime FechaEntrada { get; set; }

        [Required(ErrorMessage = "Ingrese la fecha de salida.")]
        [DataType(DataType.Date)]
        public DateTime FechaSalida { get; set; }

        [Range(1, 20, ErrorMessage = "Ingrese una cantidad válida.")]
        public int CantidadPersonas { get; set; } = 1;

        public decimal TotalReserva { get; set; } = 0;

        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Ingrese un monto válido.")]
        public decimal MontoPagoInicial { get; set; } = 0;

        public int? IdFormaPagoInicial { get; set; }

        public string? Observaciones { get; set; }
    }
}