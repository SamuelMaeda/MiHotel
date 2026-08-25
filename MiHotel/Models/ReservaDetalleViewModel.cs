namespace MiHotel.Models
{
    public class ReservaDetalleViewModel
    {
        public int IdReserva { get; set; }
        public int? IdReservaGrupo { get; set; }
        public string Cliente { get; set; } = "";
        public string? EmpresaProcedencia { get; set; }
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public DateTime? FechaHoraCheckIn { get; set; }
        public DateTime? FechaHoraCheckOut { get; set; }
        public int CantidadPersonas { get; set; }
        public decimal TotalReserva { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string Estado { get; set; } = "";
        public string? Observaciones { get; set; }

        public string? CodigoSeguridad { get; set; }

        public List<ReservaGrupoItemViewModel> ReservasDelGrupo { get; set; } = new();
    }

    public class ReservaGrupoItemViewModel
    {
        public int IdReserva { get; set; }
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public decimal TotalReserva { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string Estado { get; set; } = "";
    }
}
