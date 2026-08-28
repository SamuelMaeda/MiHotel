namespace MiHotel.Models
{
    public class CuentaPorCobrarDetalleViewModel
    {
        public int IdReserva { get; set; }
        public int? IdReservaGrupo { get; set; }
        public int? IdReservaRetorno { get; set; }
        public bool EsCuentaAgrupada => IdReservaGrupo.HasValue;
        public string Cliente { get; set; } = "";
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public string EstadoReserva { get; set; } = "";
        public decimal SaldoPendiente { get; set; }
        public string EstadoFacturacion { get; set; } = "sin_definir";
        public List<MovimientoCuentaViewModel> Movimientos { get; set; } = new();
        public List<ReservaGrupoItemViewModel> EstadiasAgrupadas { get; set; } = new();
    }
}
