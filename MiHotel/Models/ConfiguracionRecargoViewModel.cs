using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ConfiguracionRecargoViewModel
    {
        [Display(Name = "Recargo por persona y noche al pagar con tarjeta")]
        [Range(0, 99999.99, ErrorMessage = "El recargo debe estar entre Q0.00 y Q99,999.99.")]
        public decimal RecargoTarjeta { get; set; } = 25m;

        [Display(Name = "IVA")]
        [Range(0, 100, ErrorMessage = "El porcentaje de IVA debe estar entre 0% y 100%.")]
        public decimal IvaPorcentaje { get; set; } = 12m;

        [Display(Name = "Impuesto de turismo INGUAT")]
        [Range(0, 100, ErrorMessage = "El porcentaje de INGUAT debe estar entre 0% y 100%.")]
        public decimal ImpuestoTurismoPorcentaje { get; set; } = 10m;

        public DateTime? FechaActualizacion { get; set; }
    }
}
