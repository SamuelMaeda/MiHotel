namespace MiHotel.Models
{
    public class ReservaDetalleViewModel
    {
        public int IdReserva { get; set; }
        public int? IdReservaGrupo { get; set; }
        public string Cliente { get; set; } = "";
        public string? NitCliente { get; set; }
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

        public bool? RequiereFactura { get; set; }
        public string EstadoFacturacion { get; set; } = "sin_definir";
        public string EstadoAdministrativo { get; set; } = "pendiente_revision";
        public bool EsAdministrador { get; set; }
        public List<DocumentoFiscalViewModel> DocumentosFiscales { get; set; } = new();
        public List<FacturacionHistorialViewModel> HistorialFacturacion { get; set; } = new();

        public List<ReservaGrupoItemViewModel> ReservasDelGrupo { get; set; } = new();
    }

    public class ReservaGrupoItemViewModel
    {
        public int IdReserva { get; set; }
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public int CantidadPersonas { get; set; } = 1;
        public decimal TotalReserva { get; set; }
        public decimal SaldoPendiente { get; set; }
        public bool RecargoTarjetaAplicado { get; set; }
        public string Estado { get; set; } = "";
    }
}
