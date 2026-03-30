namespace MiHotel.Models
{
    public class ReservaDetalleViewModel
    {
        public int IdReserva { get; set; }
        public string Cliente { get; set; } = "";
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public int CantidadPersonas { get; set; }
        public decimal Anticipo { get; set; }
        public decimal TotalPagadoAdicional { get; set; }
        public decimal TotalPagadoGeneral { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string Estado { get; set; } = "";
        public string? Observaciones { get; set; }
    }
}