using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class HabitacionFormViewModel
    {
        public int IdProser { get; set; }

        [Required(ErrorMessage = "El número de habitación es obligatorio.")]
        [StringLength(20, ErrorMessage = "El número de habitación no puede exceder 20 caracteres.")]
        [Display(Name = "Número de habitación")]
        public string NumeroHabitacion { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar el tipo de habitación.")]
        [Display(Name = "Tipo de habitación")]
        public int IdSubcategoria { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el estado de la habitación.")]
        [Display(Name = "Estado")]
        public int IdTipoEstado { get; set; }

        [StringLength(255, ErrorMessage = "La descripción no puede exceder 255 caracteres.")]
        public string? Descripcion { get; set; }
    }
}