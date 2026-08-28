namespace MiHotel.Models
{
    public class MovimientoCuentaViewModel
    {
        public int IdMovimiento { get; set; }
        public string Tipo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string FormaPago { get; set; } = "";
        public decimal Monto { get; set; }
        public decimal RecargoTarjeta { get; set; }
        public DateTime FechaHora { get; set; }
        public string Estado { get; set; } = "";
        public string Observaciones { get; set; } = "";
        public bool EsAbono { get; set; }

        public decimal TotalCobrado => Monto + RecargoTarjeta;

        public bool EsCuentaPorCobrar =>
            Tipo.Trim().Replace("_", " ").Equals("cuenta por cobrar", StringComparison.OrdinalIgnoreCase);

        public bool EsReembolso =>
            Tipo.Trim().Equals("reembolso", StringComparison.OrdinalIgnoreCase);

        public string MovimientoLegible
        {
            get
            {
                if (EsCuentaPorCobrar)
                {
                    return "Reserva creada";
                }

                if (EsAbono)
                {
                    return Estado.Trim().Equals("activo", StringComparison.OrdinalIgnoreCase)
                        ? "Pago registrado"
                        : "Pago anulado";
                }

                if (EsReembolso)
                {
                    return "Reembolso registrado";
                }

                return string.IsNullOrWhiteSpace(Descripcion)
                    ? Tipo.Replace("_", " ")
                    : Descripcion;
            }
        }

        public string FormaPagoLegible => EsCuentaPorCobrar || string.IsNullOrWhiteSpace(FormaPago)
            ? "—"
            : FormaPago;
    }
}
