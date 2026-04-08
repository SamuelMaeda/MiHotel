using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Services;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class DisponibilidadController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly DisponibilidadService _disponibilidadService;

        public DisponibilidadController(
            ConexionBD conexionBD,
            DisponibilidadService disponibilidadService)
        {
            _conexionBD = conexionBD;
            _disponibilidadService = disponibilidadService;
        }

        // ===============================
        // VALIDAR SESION ACTIVA
        // ===============================
        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrEmpty(idUsuario);
        }

        // ===============================
        // VALIDAR SESION
        // ===============================
        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            return null;
        }

        // ===============================
        // OBTENER ID DE TIPO PROSER HABITACION
        // ===============================
        private int ObtenerIdTipoProserHabitacion(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_tipoproser
                FROM tipo_proser
                WHERE LOWER(nombre) = 'habitacion'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe el tipo 'habitacion' en tipo_proser.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // CARGAR TIPOS DE HABITACION DINAMICOS
        // ===============================
        private void CargarTiposHabitacion()
        {
            List<dynamic> tiposHabitacion = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string consulta = @"
                    SELECT DISTINCT
                        s.id_subcategoria,
                        s.nombre_subcategoria
                    FROM proser p
                    INNER JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                    WHERE p.id_tipoproser = @id_tipoproser
                      AND p.id_subcategoria IS NOT NULL
                    ORDER BY s.nombre_subcategoria;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    tiposHabitacion.Add(new
                    {
                        IdSubcategoria = Convert.ToInt32(lector["id_subcategoria"]),
                        Nombre = lector["nombre_subcategoria"]?.ToString() ?? ""
                    });
                }
            }
            catch
            {
            }

            ViewBag.TiposHabitacion = tiposHabitacion;
        }

        // ===============================
        // VISTA INICIAL DE DISPONIBILIDAD
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index()
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarTiposHabitacion();

            DisponibilidadConsultaViewModel modelo = new DisponibilidadConsultaViewModel();

            return View(modelo);
        }

        // ===============================
        // CONSULTAR DISPONIBILIDAD
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Consultar(DisponibilidadConsultaViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarTiposHabitacion();

            modelo.Consultado = true;

            if (!ModelState.IsValid)
            {
                return View("Index", modelo);
            }

            if (!modelo.FechaEntrada.HasValue || !modelo.FechaSalida.HasValue)
            {
                ModelState.AddModelError("", "Debe ingresar la fecha de entrada y la fecha de salida.");
                return View("Index", modelo);
            }

            if (modelo.FechaEntrada.Value.Date < DateTime.Today)
            {
                ModelState.AddModelError("", "La fecha de entrada no puede ser menor a hoy.");
                return View("Index", modelo);
            }

            if (modelo.FechaSalida.Value.Date <= modelo.FechaEntrada.Value.Date)
            {
                ModelState.AddModelError("", "La fecha de salida debe ser mayor que la fecha de entrada.");
                return View("Index", modelo);
            }

            try
            {
                modelo.HabitacionesDisponibles = _disponibilidadService.ObtenerHabitacionesDisponibles(
                    modelo.FechaEntrada.Value,
                    modelo.FechaSalida.Value,
                    modelo.IdSubcategoria
                );
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al consultar la disponibilidad: " + ex.Message;
            }

            return View("Index", modelo);
        }
    }
}