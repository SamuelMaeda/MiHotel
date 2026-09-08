// ===============================
// MODELO DE INICIO DE SESION
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class InicioSesion
    {
        // ===============================
        // CORREO ELECTRONICO O NOMBRE DE USUARIO
        // ===============================

        [Required(ErrorMessage = "El correo o nombre de usuario es obligatorio.")]
        [Display(Name = "Correo o nombre de usuario")]
        public string Identificador { get; set; } = string.Empty;

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
