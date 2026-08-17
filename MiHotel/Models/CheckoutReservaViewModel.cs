namespace MiHotel.Models
{
    public class CheckoutReservaViewModel
    {
        public int IdReserva { get; set; }
        public string Cliente { get; set; } = "";
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public decimal TotalReserva { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string Estado { get; set; } = "";
        public string Observaciones { get; set; } = "";
        public bool EsAdministrador { get; set; }
        public List<MovimientoCuentaViewModel> Movimientos { get; set; } = new();
    }
}
