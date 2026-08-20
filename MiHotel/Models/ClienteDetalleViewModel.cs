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
        // Se conserva únicamente para mostrar la empresa del proveedor.
        public string? NombreEmpresa { get; set; }
        public string? EmpresaProcedencia { get; set; }
        public string? NumeroDpi { get; set; }
        public string? PlacaReciente { get; set; }
        public string CodigoClasificacion { get; set; } = "B";
        public string NombreClasificacion { get; set; } = "Neutral";
        public bool TieneDpiFrente { get; set; }
        public string Estado { get; set; } = "activo";
    }
}
