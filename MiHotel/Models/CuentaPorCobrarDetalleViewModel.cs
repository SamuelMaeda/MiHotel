namespace MiHotel.Models
{
    public class CuentaPorCobrarDetalleViewModel
    {
        public int IdReserva { get; set; }
        public string Cliente { get; set; } = "";
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public string EstadoReserva { get; set; } = "";
        public decimal SaldoPendiente { get; set; }
        public List<MovimientoCuentaViewModel> Movimientos { get; set; } = new();
    }
}
