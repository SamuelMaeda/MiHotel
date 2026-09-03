using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class ReporteGeneralViewModel
    {
        [Display(Name = "Fecha inicial")]
        [DataType(DataType.Date)]
        public DateTime FechaInicio { get; set; }

        [Display(Name = "Fecha final")]
        [DataType(DataType.Date)]
        public DateTime FechaFin { get; set; }

        public bool EsAdministrador { get; set; }
        public int TotalReservas { get; set; }
        public int ReservasFinalizadas { get; set; }
        public int ReservasCanceladas { get; set; }
        public int PersonasRegistradas { get; set; }
        public int HabitacionesActivas { get; set; }
        public int NochesDisponibles { get; set; }
        public int NochesReservadas { get; set; }
        public decimal PorcentajeOcupacion { get; set; }
        public decimal IngresosHospedaje { get; set; }
        public decimal IngresosPuntoVenta { get; set; }
        public decimal IngresosTotales => IngresosHospedaje + IngresosPuntoVenta;
        public decimal SaldoPendiente { get; set; }
        public List<ReporteEstadoReservaViewModel> ReservasPorEstado { get; set; } = new();
        public List<ReporteHabitacionViewModel> HabitacionesMasUtilizadas { get; set; } = new();
        public List<ReporteClienteFrecuenteViewModel> ClientesFrecuentes { get; set; } = new();
        public List<ReporteEmpresaFrecuenteViewModel> EmpresasFrecuentes { get; set; } = new();
        public List<ReporteFechaActividadViewModel> FechasMayorActividad { get; set; } = new();
        public List<ReporteProductoVendidoViewModel> ProductosMasVendidos { get; set; } = new();
    }

    public class ReporteEstadoReservaViewModel
    {
        public string Estado { get; set; } = "";
        public int Cantidad { get; set; }
    }

    public class ReporteHabitacionViewModel
    {
        public string Habitacion { get; set; } = "";
        public int NochesReservadas { get; set; }
        public decimal PorcentajeUso { get; set; }
    }

    public class ReporteProductoVendidoViewModel
    {
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public int CantidadVendida { get; set; }
        public decimal TotalVendido { get; set; }
    }

    public class ReporteClienteFrecuenteViewModel
    {
        public string Cliente { get; set; } = "";
        public int NochesReservadas { get; set; }
        public DateTime UltimaEntrada { get; set; }
    }

    public class ReporteEmpresaFrecuenteViewModel
    {
        public string Empresa { get; set; } = "";
        public int Clientes { get; set; }
        public int NochesReservadas { get; set; }
    }

    public class ReporteFechaActividadViewModel
    {
        public DateTime Fecha { get; set; }
        public int Llegadas { get; set; }
        public int Personas { get; set; }
    }
}
