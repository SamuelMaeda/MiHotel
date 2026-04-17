// ===============================
// VIEWMODEL DE DETALLE DE PRODUCTO O SERVICIO
// ===============================

namespace MiHotel.Models
{
    public class ProductoServicioDetalleViewModel
    {
        public int IdProser { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string NombreProser { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public string? Categoria { get; set; }

        public string? Subcategoria { get; set; }

        public string? Marca { get; set; }

        public string? UnidadMedida { get; set; }

        public decimal Precio { get; set; }

        public int Stock { get; set; }

        public string Estado { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public bool EsServicio { get; set; }
    }
}