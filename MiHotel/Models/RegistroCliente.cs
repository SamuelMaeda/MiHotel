// ===============================
// MODELO DE REGISTRO DE CLIENTE
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class RegistroCliente
    {
        // ===============================
        // NOMBRE DEL CLIENTE
        // ===============================

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        // ===============================
        // CORREO ELECTRONICO
        // ===============================

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe ingresar un correo válido.")]
        public string Correo { get; set; } = string.Empty;

        // ===============================
        // TELEFONO
        // ===============================

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [StringLength(20)]
        public string Telefono { get; set; } = string.Empty;

        // ===============================
        // CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string Clave { get; set; } = string.Empty;

        // ===============================
        // CONFIRMACION DE CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "Debe confirmar la contraseña.")]
        [Compare("Clave", ErrorMessage = "Las contraseñas no coinciden.")]
        [DataType(DataType.Password)]
        public string ConfirmarClave { get; set; } = string.Empty;
    }
}