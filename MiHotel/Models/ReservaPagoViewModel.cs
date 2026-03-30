using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ReservaPagoViewModel
    {
        public int IdReserva { get; set; }

        [Required(ErrorMessage = "Seleccione una forma de pago.")]
        public int IdFormaPago { get; set; }

        [Required(ErrorMessage = "Ingrese el monto.")]
        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Ingrese un monto válido.")]
        public decimal Monto { get; set; }

        public string? Observaciones { get; set; }
    }
}