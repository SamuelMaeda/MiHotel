namespace MiHotel.Models
{
    public class ClienteDetalleViewModel
    {
        public int IdClipro { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Nit { get; set; }
        public string Telefono { get; set; } = string.Empty;
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public string? NombreEmpresa { get; set; }
        public string? NumeroEmpresa { get; set; }
        public string Estado { get; set; } = "activo";
    }
}