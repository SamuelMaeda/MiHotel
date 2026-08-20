using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class EmpresaClienteViewModel
    {
        public int IdEmpresaCliente { get; set; }

        [Required(ErrorMessage = "El nombre de la empresa es obligatorio.")]
        [StringLength(150)]
        [Display(Name = "Nombre de la empresa")]
        public string Nombre { get; set; } = string.Empty;

        public string Estado { get; set; } = "activo";
    }
}
