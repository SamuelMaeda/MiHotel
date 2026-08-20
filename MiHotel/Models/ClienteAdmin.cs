using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ClienteAdmin
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Nit { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [RegularExpression(@"^\d{4}\s?\d{4}$", ErrorMessage = "Ingrese un teléfono válido de 8 dígitos.")]
        public string Telefono { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [StringLength(150)]
        public string? Correo { get; set; }

        [StringLength(255)]
        public string? Direccion { get; set; }

        // Se conserva únicamente para el módulo de proveedores.
        [StringLength(150)]
        [Display(Name = "Empresa proveedora")]
        public string? NombreEmpresa { get; set; }

        [Display(Name = "Empresa de procedencia")]
        public int? IdEmpresaCliente { get; set; }

        [RegularExpression(@"^\d{13}$", ErrorMessage = "El DPI debe contener exactamente 13 dígitos.")]
        [Display(Name = "Número de DPI")]
        public string? NumeroDpi { get; set; }

        [StringLength(15)]
        [RegularExpression(@"^[A-Za-z0-9-]+$", ErrorMessage = "La placa solo puede contener letras, números y guiones.")]
        [Display(Name = "Última placa registrada")]
        public string? PlacaReciente { get; set; }

        [Required(ErrorMessage = "Seleccione el tipo de cliente.")]
        [RegularExpression("^[ABC]$", ErrorMessage = "Seleccione un tipo de cliente válido.")]
        [Display(Name = "Tipo de cliente")]
        public string CodigoClasificacion { get; set; } = "B";

        [Display(Name = "Fotografía frontal del DPI")]
        public IFormFile? DpiFrente { get; set; }

    }
}
