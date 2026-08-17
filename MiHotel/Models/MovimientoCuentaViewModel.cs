namespace MiHotel.Models
{
    public class MovimientoCuentaViewModel
    {
        public int IdMovimiento { get; set; }
        public string Tipo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string FormaPago { get; set; } = "";
        public decimal Monto { get; set; }
        public DateTime FechaHora { get; set; }
        public string Estado { get; set; } = "";
        public string Observaciones { get; set; } = "";
        public bool EsAbono { get; set; }
    }
}
