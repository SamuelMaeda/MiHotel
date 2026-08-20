using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class MantenimientoViewModel
    {
        public int IdMantenimiento { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(1000, ErrorMessage = "La descripción no puede superar 1,000 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        public DateTime? FechaRegistro { get; set; }
        public DateTime? FechaCompletado { get; set; }
        public string Estado { get; set; } = "pendiente";
    }
}
