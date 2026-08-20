using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class TrabajadorViewModel
    {
        public int IdTrabajador { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Código de servicio")]
        public string? CodigoServicio { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [RegularExpression(@"^\d{4}\s?\d{4}$", ErrorMessage = "Ingrese un teléfono válido de 8 dígitos.")]
        public string Telefono { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Observaciones { get; set; }

        public string Estado { get; set; } = "activo";
    }
}
