namespace MiHotel.Models
{
    public class DocumentoFiscalViewModel
    {
        public long IdDocumentoFiscal { get; set; }
        public string TipoDocumento { get; set; } = "factura";
        public string? NitReceptor { get; set; }
        public string? Serie { get; set; }
        public string? NumeroDte { get; set; }
        public string NombreOriginal { get; set; } = "";
        public long Tamano { get; set; }
        public string Estado { get; set; } = "vigente";
        public DateTime FechaRegistro { get; set; }
        public string UsuarioRegistro { get; set; } = "";
        public string? MotivoEstado { get; set; }
        public bool EsHeredado => string.IsNullOrWhiteSpace(Serie) || string.IsNullOrWhiteSpace(NumeroDte);
        public List<int> IdsReservas { get; set; } = new();
    }

    public class FacturaPendienteItemViewModel
    {
        public int IdReserva { get; set; }
        public int? IdReservaGrupo { get; set; }
        public string Cliente { get; set; } = "";
        public string? NitCliente { get; set; }
        public string Habitacion { get; set; } = "";
        public DateTime FechaEntrada { get; set; }
        public DateTime FechaSalida { get; set; }
        public DateTime? FechaDecision { get; set; }
        public decimal TotalReserva { get; set; }
        public string EstadoEstadia { get; set; } = "";
        public string EstadoFacturacion { get; set; } = "sin_definir";
        public string UsuarioDecision { get; set; } = "";
        public int CantidadDocumentos { get; set; }
    }

    public class FacturasIndexViewModel
    {
        public string Vista { get; set; } = "pendientes";
        public string Busqueda { get; set; } = "";
        public List<FacturaPendienteItemViewModel> Registros { get; set; } = new();
    }

    public class FacturacionHistorialViewModel
    {
        public long IdHistorial { get; set; }
        public string Accion { get; set; } = "";
        public string? EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; } = "";
        public string? Detalle { get; set; }
        public string Usuario { get; set; } = "";
        public DateTime FechaHora { get; set; }
    }
}
