// ===============================
// MODELO DE RESETEO DE CLAVE POR ADMINISTRADOR
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ResetearClaveAdmin
    {
        // ===============================
        // ID DEL USUARIO
        // ===============================

        [Required]
        public int IdUsuario { get; set; }

        // ===============================
        // NOMBRE DEL USUARIO
        // ===============================

        public string NombreUsuario { get; set; } = string.Empty;

        // ===============================
        // NUEVA CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string NuevaClave { get; set; } = string.Empty;

        // ===============================
        // CONFIRMAR NUEVA CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "Debe confirmar la nueva contraseña.")]
        [Compare("NuevaClave", ErrorMessage = "Las contraseñas no coinciden.")]
        [DataType(DataType.Password)]
        public string ConfirmarNuevaClave { get; set; } = string.Empty;
    }
}