// ===============================
// MODELO DE RESTABLECIMIENTO DE CLAVE
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class RestablecerClave
    {
        // ===============================
        // TOKEN DE RECUPERACION
        // ===============================

        [Required]
        public string Token { get; set; } = string.Empty;

        // ===============================
        // NUEVA CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string NuevaClave { get; set; } = string.Empty;

        // ===============================
        // CONFIRMACION DE NUEVA CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "Debe confirmar la nueva contraseña.")]
        [Compare("NuevaClave", ErrorMessage = "Las contraseñas no coinciden.")]
        [DataType(DataType.Password)]
        public string ConfirmarNuevaClave { get; set; } = string.Empty;
    }
}