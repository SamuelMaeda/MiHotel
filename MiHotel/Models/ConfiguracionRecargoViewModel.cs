using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ConfiguracionRecargoViewModel
    {
        [Display(Name = "Recargo por persona y noche al pagar con tarjeta")]
        [Range(0, 99999.99, ErrorMessage = "El recargo debe estar entre Q0.00 y Q99,999.99.")]
        public decimal RecargoTarjeta { get; set; } = 25m;

        public DateTime? FechaActualizacion { get; set; }
    }
}
