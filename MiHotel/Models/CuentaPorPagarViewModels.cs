using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class CuentaPorPagarFormViewModel
    {
        public int IdCuentaPorPagar { get; set; }

        [Required(ErrorMessage = "El nombre del pago es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
        [Display(Name = "Nombre del pago")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "La descripción no puede superar 1,000 caracteres.")]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "Seleccione el tipo de monto.")]
        [Display(Name = "Tipo de monto")]
        public string TipoMonto { get; set; } = "fijo";

        [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "El monto mensual debe ser mayor que cero.")]
        [Display(Name = "Monto mensual")]
        public decimal? MontoMensual { get; set; }

        [Range(1, 28, ErrorMessage = "El día de pago debe estar entre 1 y 28.")]
        [Display(Name = "Día de pago")]
        public int DiaVencimiento { get; set; } = 1;
    }

    public class CuentaPorPagarResumenViewModel
    {
        public int IdCuentaPorPagar { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string TipoMonto { get; set; } = "fijo";
        public decimal? MontoMensual { get; set; }
        public int DiaVencimiento { get; set; }
        public bool Activa { get; set; }
        public int MesesPendientes { get; set; }
        public int PeriodosSinMonto { get; set; }
        public decimal SaldoPendiente { get; set; }
        public DateTime? FechaUltimoPago { get; set; }
        public DateTime FechaRegistro { get; set; }
    }

    public class CuentasPorPagarIndexViewModel
    {
        public string Vista { get; set; } = "por_pagar";
        public string Busqueda { get; set; } = string.Empty;
        public string OrdenarPor { get; set; } = "nombre";
        public string Direccion { get; set; } = "asc";
        public int PaginaActual { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
        public int TotalRegistros { get; set; }
        public List<CuentaPorPagarResumenViewModel> Registros { get; set; } = new();

        public decimal SaldoTotal { get; set; }
        public int PeriodosSinMontoTotal { get; set; }
    }

    public class CuentaPorPagarPeriodoViewModel
    {
        public long IdPeriodo { get; set; }
        public DateTime Periodo { get; set; }
        public decimal? MontoGenerado { get; set; }
        public string Estado { get; set; } = "pendiente_monto";
    }

    public class CuentaPorPagarMontosViewModel
    {
        public int IdCuentaPorPagar { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public List<CuentaPorPagarPeriodoViewModel> Periodos { get; set; } = new();
    }
}
