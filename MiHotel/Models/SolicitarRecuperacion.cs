// ===============================
// MODELO DE SOLICITUD DE RECUPERACION
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class SolicitarRecuperacion
    {
        // ===============================
        // CORREO ELECTRONICO
        // ===============================

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe ingresar un correo válido.")]
        public string Correo { get; set; } = string.Empty;
    }
}