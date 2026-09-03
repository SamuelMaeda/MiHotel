using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class SimulacionPreciosController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public SimulacionPreciosController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarAccesoAdmin()
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("IdUsuario")))
            {
                return RedirectToAction("Login", "Acceso");
            }

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";

            if (rol != "admin")
            {
                TempData["Mensaje"] = "Solo los administradores pueden utilizar la simulación de precios.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index()
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null) return acceso;

            var modelo = new SimulacionPreciosViewModel();

            if (!CargarImpuestos(modelo, out string mensaje))
            {
                ViewBag.Mensaje = mensaje;
            }

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(SimulacionPreciosViewModel modelo)
        {
            IActionResult? acceso = ValidarAccesoAdmin();
            if (acceso != null) return acceso;

            if (!CargarImpuestos(modelo, out string mensaje))
            {
                ViewBag.Mensaje = mensaje;
                return View(modelo);
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            modelo.Resultados = CalcularResultados(modelo);
            modelo.Calculada = true;
            return View(modelo);
        }

        private bool CargarImpuestos(SimulacionPreciosViewModel modelo, out string mensaje)
        {
            mensaje = string.Empty;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var comando = new MySqlCommand(@"
                    SELECT iva_porcentaje, impuesto_turismo_porcentaje
                    FROM configuracion_sistema
                    WHERE id_configuracion = 1
                    LIMIT 1;", conexion);

                using var lector = comando.ExecuteReader();

                if (lector.Read())
                {
                    modelo.IvaPorcentaje = Convert.ToDecimal(lector["iva_porcentaje"]);
                    modelo.ImpuestoTurismoPorcentaje = Convert.ToDecimal(lector["impuesto_turismo_porcentaje"]);
                }

                return true;
            }
            catch (Exception ex)
            {
                mensaje = "No se pudieron cargar los impuestos configurados: " + ex.Message;
                return false;
            }
        }

        private static List<ResultadoEscenarioViewModel> CalcularResultados(SimulacionPreciosViewModel modelo)
        {
            var escenarios = modelo.CantidadEscenarios == 5
                ? new List<(string Nombre, decimal Ocupacion)>
                {
                    ("Bajo", modelo.OcupacionBaja),
                    ("Medio bajo", modelo.OcupacionMediaBaja),
                    ("Medio", modelo.OcupacionMedia),
                    ("Medio alto", modelo.OcupacionMediaAlta),
                    ("Alto", modelo.OcupacionAlta)
                }
                : new List<(string Nombre, decimal Ocupacion)>
                {
                    ("Bajo", modelo.OcupacionBaja),
                    ("Medio", modelo.OcupacionMedia),
                    ("Alto", modelo.OcupacionAlta)
                };

            decimal factorImpuestos = 1m + ((modelo.IvaPorcentaje + modelo.ImpuestoTurismoPorcentaje) / 100m);
            var resultados = new List<ResultadoEscenarioViewModel>();

            foreach ((string nombre, decimal ocupacion) in escenarios)
            {
                int hospedajesEstimados = (int)Math.Round(
                    modelo.HospedajesDisponibles * ocupacion / 100m,
                    MidpointRounding.AwayFromZero);

                decimal ingresoBruto = Math.Round(
                    hospedajesEstimados * modelo.PersonasPorHabitacion * modelo.TarifaPorPersona,
                    2,
                    MidpointRounding.AwayFromZero);

                decimal ingresoDespuesImpuestos = factorImpuestos > 0
                    ? Math.Round(ingresoBruto / factorImpuestos, 2, MidpointRounding.AwayFromZero)
                    : ingresoBruto;

                resultados.Add(new ResultadoEscenarioViewModel
                {
                    Escenario = nombre,
                    OcupacionPorcentaje = ocupacion,
                    HospedajesEstimados = hospedajesEstimados,
                    IngresoBruto = ingresoBruto,
                    ImpuestosEstimados = ingresoBruto - ingresoDespuesImpuestos,
                    IngresoDespuesImpuestos = ingresoDespuesImpuestos
                });
            }

            return resultados;
        }
    }
}
