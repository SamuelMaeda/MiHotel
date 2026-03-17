// ===============================
// CONFIGURACION DE CORREO
// ===============================

namespace MiHotel.Models.Configuracion
{
    public class CorreoConfig
    {
        public string Servidor { get; set; } = string.Empty;
        public int Puerto { get; set; }
        public string Remitente { get; set; } = string.Empty;
        public string ClaveAplicacion { get; set; } = string.Empty;
        public bool UsarSsl { get; set; }
        public string NombreMostrar { get; set; } = string.Empty;
    }
}