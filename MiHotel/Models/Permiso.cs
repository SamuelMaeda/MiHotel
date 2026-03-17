using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class Permiso
    {
        public int IdPermiso { get; set; }

        [Required(ErrorMessage = "El nombre del permiso es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre del permiso no puede exceder 100 caracteres.")]
        [Display(Name = "Nombre del permiso")]
        public string NombrePermiso { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "La descripción no puede exceder 255 caracteres.")]
        public string? Descripcion { get; set; }

        public bool Estado { get; set; } = true;
    }
}