using System.ComponentModel.DataAnnotations;

namespace MiHotel.Models
{
    public class SimulacionPreciosViewModel : IValidatableObject
    {
        [Display(Name = "Habitaciones a analizar")]
        [Range(1, 10000, ErrorMessage = "Ingrese una cantidad de habitaciones entre 1 y 10,000.")]
        public int CantidadHabitaciones { get; set; } = 12;

        [Display(Name = "Duración del periodo")]
        [Range(1, 365, ErrorMessage = "El periodo debe tener entre 1 y 365 noches.")]
        public int CantidadNoches { get; set; } = 30;

        [Display(Name = "Tarifa por persona y noche")]
        [Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "Ingrese una tarifa mayor que Q0.00.")]
        public decimal TarifaPorPersona { get; set; } = 250m;

        [Display(Name = "Personas estimadas por habitación")]
        [Range(1, 20, ErrorMessage = "Ingrese entre 1 y 20 personas.")]
        public int PersonasPorHabitacion { get; set; } = 1;

        [Display(Name = "Cantidad de escenarios")]
        public int CantidadEscenarios { get; set; } = 3;

        [Display(Name = "Ocupación baja")]
        [Range(typeof(decimal), "0", "100", ErrorMessage = "La ocupación debe estar entre 0% y 100%.")]
        public decimal OcupacionBaja { get; set; } = 30m;

        [Display(Name = "Ocupación media baja")]
        [Range(typeof(decimal), "0", "100", ErrorMessage = "La ocupación debe estar entre 0% y 100%.")]
        public decimal OcupacionMediaBaja { get; set; } = 35m;

        [Display(Name = "Ocupación media")]
        [Range(typeof(decimal), "0", "100", ErrorMessage = "La ocupación debe estar entre 0% y 100%.")]
        public decimal OcupacionMedia { get; set; } = 55m;

        [Display(Name = "Ocupación media alta")]
        [Range(typeof(decimal), "0", "100", ErrorMessage = "La ocupación debe estar entre 0% y 100%.")]
        public decimal OcupacionMediaAlta { get; set; } = 70m;

        [Display(Name = "Ocupación alta")]
        [Range(typeof(decimal), "0", "100", ErrorMessage = "La ocupación debe estar entre 0% y 100%.")]
        public decimal OcupacionAlta { get; set; } = 80m;

        public decimal IvaPorcentaje { get; set; } = 12m;
        public decimal ImpuestoTurismoPorcentaje { get; set; } = 10m;
        public bool Calculada { get; set; }
        public List<ResultadoEscenarioViewModel> Resultados { get; set; } = new();

        public int HospedajesDisponibles => CantidadHabitaciones * CantidadNoches;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (CantidadEscenarios is not 3 and not 5)
            {
                yield return new ValidationResult(
                    "Seleccione una simulación de 3 o 5 escenarios.",
                    new[] { nameof(CantidadEscenarios) });
                yield break;
            }

            decimal[] ocupaciones = CantidadEscenarios == 5
                ? new[] { OcupacionBaja, OcupacionMediaBaja, OcupacionMedia, OcupacionMediaAlta, OcupacionAlta }
                : new[] { OcupacionBaja, OcupacionMedia, OcupacionAlta };

            if (ocupaciones.Zip(ocupaciones.Skip(1), (actual, siguiente) => actual < siguiente).Any(esValido => !esValido))
            {
                yield return new ValidationResult(
                    "Los porcentajes deben aumentar desde el escenario bajo hasta el escenario alto.");
            }
        }
    }

    public class ResultadoEscenarioViewModel
    {
        public string Escenario { get; set; } = string.Empty;
        public decimal OcupacionPorcentaje { get; set; }
        public int HospedajesEstimados { get; set; }
        public decimal IngresoBruto { get; set; }
        public decimal ImpuestosEstimados { get; set; }
        public decimal IngresoDespuesImpuestos { get; set; }
    }
}
