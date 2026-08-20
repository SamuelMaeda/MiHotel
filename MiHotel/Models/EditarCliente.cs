using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class EditarCliente : ClienteAdmin
    {
        [Required]
        public int IdClipro { get; set; }

        public bool TieneDpiFrente { get; set; }
    }
}
