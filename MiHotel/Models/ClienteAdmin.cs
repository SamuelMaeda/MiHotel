using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ClienteAdmin : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "NIT")]
        public string? Nit { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [RegularExpression(@"^\d{4}\s?\d{4}$", ErrorMessage = "Ingrese un teléfono válido de 8 dígitos.")]
        [Display(Name = "Teléfono")]
        public string Telefono { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [StringLength(150)]
        [Display(Name = "Correo")]
        public string? Correo { get; set; }

        [StringLength(255)]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        [StringLength(150)]
        [Display(Name = "Nombre de empresa")]
        public string? NombreEmpresa { get; set; }

        [RegularExpression(@"^\d{4}\s?\d{4}$", ErrorMessage = "Ingrese un número de empresa válido de 8 dígitos.")]
        [Display(Name = "Número de empresa")]
        public string? NumeroEmpresa { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            bool tieneNombreEmpresa = !string.IsNullOrWhiteSpace(NombreEmpresa);
            bool tieneNumeroEmpresa = !string.IsNullOrWhiteSpace(NumeroEmpresa);

            if (tieneNombreEmpresa && !tieneNumeroEmpresa)
            {
                yield return new ValidationResult(
                    "Debe ingresar el número de la empresa cuando se indique el nombre de la empresa.",
                    new[] { nameof(NumeroEmpresa) });
            }

            if (!tieneNombreEmpresa && tieneNumeroEmpresa)
            {
                yield return new ValidationResult(
                    "Debe ingresar el nombre de la empresa cuando se indique el número de la empresa.",
                    new[] { nameof(NombreEmpresa) });
            }
        }
    }
}