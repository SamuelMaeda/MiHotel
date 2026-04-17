// ===============================
// VIEWMODEL DE PRODUCTOS Y SERVICIOS
// ===============================

using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ProductoServicioFormViewModel
    {
        public int IdProser { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el tipo.")]
        [Display(Name = "Tipo")]
        public int IdTipoProser { get; set; }

        [Display(Name = "Código")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
        [Display(Name = "Nombre")]
        public string NombreProser { get; set; } = string.Empty;

        [Display(Name = "Categoría")]
        public int? IdCategoria { get; set; }

        [Display(Name = "Subcategoría")]
        public int? IdSubcategoria { get; set; }

        [Display(Name = "Marca")]
        public int? IdMarca { get; set; }

        [Display(Name = "Unidad de medida")]
        public int? IdUnidadMedida { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el estado.")]
        [Display(Name = "Estado")]
        public int IdTipoEstado { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "El precio debe ser mayor que cero.")]
        [Display(Name = "Precio")]
        public decimal Precio { get; set; }

        [Range(0, 999999, ErrorMessage = "El stock no puede ser negativo.")]
        [Display(Name = "Stock")]
        public int Stock { get; set; }

        [StringLength(255, ErrorMessage = "La descripción no puede exceder 255 caracteres.")]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        public bool EsServicio { get; set; }
    }
}