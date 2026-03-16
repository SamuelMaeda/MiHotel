// ===============================
// MODELO DE REGISTRO ADMINISTRATIVO DE USUARIO
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class UsuarioAdmin
    {
        // ===============================
        // NOMBRE DEL USUARIO
        // ===============================

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no debe exceder los 100 caracteres.")]
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
        [RegularExpression(@"^\d{4}\s?\d{4}$", ErrorMessage = "El teléfono debe tener 8 dígitos válidos de Guatemala.")]
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

        // ===============================
        // ROL
        // ===============================

        [Required(ErrorMessage = "Debe seleccionar un rol.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un rol válido.")]
        public int IdRol { get; set; }

        // ===============================
        // ESTADO
        // ===============================

        [Required(ErrorMessage = "Debe seleccionar un estado.")]
        public string Estado { get; set; } = "activo";
    }
}