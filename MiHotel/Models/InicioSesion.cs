// ===============================
// MODELO DE INICIO DE SESION
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class InicioSesion
    {
        // ===============================
        // CORREO ELECTRONICO
        // ===============================

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe ingresar un correo válido.")]
        public string Correo { get; set; } = string.Empty;

        // ===============================
        // CONTRASEÑA
        // ===============================

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Clave { get; set; } = string.Empty;

        // ===============================
        // RECORDAR SESION
        // ===============================

        public bool Recordarme { get; set; }
    }
}