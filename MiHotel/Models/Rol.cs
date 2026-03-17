// ===============================
// MODELO DE ROL
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class Rol
    {
        // ===============================
        // ID DEL ROL
        // ===============================

        public int IdRol { get; set; }

        // ===============================
        // NOMBRE DEL ROL
        // ===============================

        [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
        [StringLength(50)]
        public string NombreRol { get; set; } = string.Empty;

        // ===============================
        // ESTADO
        // ===============================

        [Required]
        public string Estado { get; set; } = "activo";
    }
}