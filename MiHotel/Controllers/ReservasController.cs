// ===============================
// CONTROLADOR DE RESERVAS
// ===============================

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class ReservasController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public ReservasController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
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
        // OBTENER ID DE TIPO CLIENTE
        // ===============================
        private int ObtenerIdTipoCliente(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_tipoclipro
                FROM tipo_clipro
                WHERE LOWER(tipo) = 'cliente'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe el tipo 'cliente' en tipo_clipro.");
            }

            return Convert.ToInt32(resultado);
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
                throw new Exception("No existe el tipo_proser 'habitacion'.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // OBTENER COLUMNA DE ORDEN SEGURA
        // ===============================
        private string ObtenerColumnaOrden(string columna)
        {
            return columna.Trim().ToLower() switch
            {
                "cliente" => "c.nombre",
                "fecha_entrada" => "r.fecha_entrada",
                _ => "r.fecha_entrada"
            };
        }

        // ===============================
        // INDEX DE RESERVAS
        // ===============================
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "fecha_entrada",
            string direccion = "asc",
            string vista = "pendiente",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaReservas = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string vistaNormalizada = vista.Trim().ToLower();
                string[] estadosPermitidos = { "pendiente", "confirmada", "cancelada", "finalizada" };

                if (!estadosPermitidos.Contains(vistaNormalizada))
                {
                    vistaNormalizada = "pendiente";
                }

                string columnaOrden = ObtenerColumnaOrden(ordenarPor);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    condicionBusqueda = @"
                        AND (
                            c.nombre LIKE @busqueda
                            OR p.codigo LIKE @busqueda
                            OR r.observaciones LIKE @busqueda
                        ) ";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    WHERE r.estado = @estado
                    {condicionBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);
                comandoConteo.Parameters.AddWithValue("@estado", vistaNormalizada);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comandoConteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                int totalRegistros = Convert.ToInt32(comandoConteo.ExecuteScalar());
                int totalPaginas = (int)Math.Ceiling((double)totalRegistros / RegistrosPorPagina);

                if (totalPaginas == 0)
                {
                    totalPaginas = 1;
                }

                if (pagina > totalPaginas)
                {
                    pagina = totalPaginas;
                }

                int offset = (pagina - 1) * RegistrosPorPagina;

                string consulta = $@"
                    SELECT
                        r.id_reserva,
                        c.nombre,
                        p.codigo AS habitacion,
                        r.fecha_entrada,
                        r.fecha_salida,
                        r.cantidad_personas,
                        r.anticipo,
                        r.saldo_pendiente,
                        r.estado,
                        r.observaciones
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    WHERE r.estado = @estado
                    {condicionBusqueda}
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@estado", vistaNormalizada);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tablaReservas);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = direccionOrden.ToLower();
                ViewBag.Vista = vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar las reservas: " + ex.Message;
            }

            return View(tablaReservas);
        }

        // ===============================
        // CREAR RESERVA - VISTA
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarCombos();
            return View(new ReservaFormViewModel());
        }

        // ===============================
        // CREAR RESERVA - GUARDAR
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(ReservaFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarCombos();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                // ===============================
                // VALIDAR FECHAS
                // ===============================
                if (modelo.FechaEntrada.Date < DateTime.Today)
                {
                    ModelState.AddModelError("", "La fecha de entrada no puede ser menor a hoy.");
                    return View(modelo);
                }

                if (modelo.FechaSalida.Date <= modelo.FechaEntrada.Date)
                {
                    ModelState.AddModelError("", "La fecha de salida debe ser mayor que la fecha de entrada.");
                    return View(modelo);
                }

                int noches = (modelo.FechaSalida.Date - modelo.FechaEntrada.Date).Days;

                if (noches <= 0)
                {
                    ModelState.AddModelError("", "La reserva debe tener al menos una noche.");
                    return View(modelo);
                }

                if (modelo.Anticipo < 0)
                {
                    ModelState.AddModelError("", "El anticipo no puede ser negativo.");
                    return View(modelo);
                }

                // ===============================
                // VALIDAR QUE CLIENTE EXISTA Y SEA ACTIVO
                // ===============================
                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string validarCliente = @"
                    SELECT COUNT(*)
                    FROM clipro
                    WHERE id_clipro = @id_clipro
                      AND id_tipoclipro = @id_tipoclipro
                      AND estado = 'activo';";

                using (var comandoCliente = new MySqlCommand(validarCliente, conexion))
                {
                    comandoCliente.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                    comandoCliente.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                    int existeCliente = Convert.ToInt32(comandoCliente.ExecuteScalar());

                    if (existeCliente == 0)
                    {
                        ModelState.AddModelError("", "El cliente seleccionado no es válido o está inactivo.");
                        return View(modelo);
                    }
                }

                // ===============================
                // VALIDAR QUE HABITACION EXISTA
                // ===============================
                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string validarHabitacion = @"
                    SELECT COUNT(*)
                    FROM proser
                    WHERE id_proser = @id_habitacion
                      AND id_tipoproser = @id_tipoproser;";

                using (var comandoHabitacion = new MySqlCommand(validarHabitacion, conexion))
                {
                    comandoHabitacion.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                    comandoHabitacion.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                    int existeHabitacion = Convert.ToInt32(comandoHabitacion.ExecuteScalar());

                    if (existeHabitacion == 0)
                    {
                        ModelState.AddModelError("", "La habitación seleccionada no es válida.");
                        return View(modelo);
                    }
                }

                // ===============================
                // VALIDAR DISPONIBILIDAD
                // ===============================
                string validarDisponibilidad = @"
                    SELECT COUNT(*)
                    FROM reserva
                    WHERE id_habitacion = @id_habitacion
                      AND estado IN ('pendiente', 'confirmada')
                      AND (
                            @fecha_entrada < fecha_salida
                            AND @fecha_salida > fecha_entrada
                          );";

                using (var comandoDisponibilidad = new MySqlCommand(validarDisponibilidad, conexion))
                {
                    comandoDisponibilidad.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                    comandoDisponibilidad.Parameters.AddWithValue("@fecha_entrada", modelo.FechaEntrada.Date);
                    comandoDisponibilidad.Parameters.AddWithValue("@fecha_salida", modelo.FechaSalida.Date);

                    int existeCruce = Convert.ToInt32(comandoDisponibilidad.ExecuteScalar());

                    if (existeCruce > 0)
                    {
                        ModelState.AddModelError("", "La habitación ya está reservada en esas fechas.");
                        return View(modelo);
                    }
                }

                // ===============================
                // OBTENER PRECIO DE HABITACION
                // ===============================
                string consultaPrecio = @"
                    SELECT precio
                    FROM proser
                    WHERE id_proser = @id_habitacion
                    LIMIT 1;";

                decimal precioPorNoche = 0;

                using (var comandoPrecio = new MySqlCommand(consultaPrecio, conexion))
                {
                    comandoPrecio.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);

                    object? resultadoPrecio = comandoPrecio.ExecuteScalar();

                    if (resultadoPrecio != null && resultadoPrecio != DBNull.Value)
                    {
                        precioPorNoche = Convert.ToDecimal(resultadoPrecio);
                    }
                }

                decimal totalReserva = precioPorNoche * noches;
                decimal saldoPendiente = totalReserva - modelo.Anticipo;

                if (saldoPendiente < 0)
                {
                    ModelState.AddModelError("", "El anticipo no puede ser mayor al total estimado de la reserva.");
                    return View(modelo);
                }

                // ===============================
                // INSERTAR RESERVA
                // ===============================
                string insertar = @"
                    INSERT INTO reserva
                    (
                        id_clipro,
                        id_habitacion,
                        fecha_entrada,
                        fecha_salida,
                        cantidad_personas,
                        anticipo,
                        saldo_pendiente,
                        estado,
                        observaciones
                    )
                    VALUES
                    (
                        @id_clipro,
                        @id_habitacion,
                        @fecha_entrada,
                        @fecha_salida,
                        @cantidad_personas,
                        @anticipo,
                        @saldo_pendiente,
                        'pendiente',
                        @observaciones
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                comandoInsertar.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                comandoInsertar.Parameters.AddWithValue("@fecha_entrada", modelo.FechaEntrada.Date);
                comandoInsertar.Parameters.AddWithValue("@fecha_salida", modelo.FechaSalida.Date);
                comandoInsertar.Parameters.AddWithValue("@cantidad_personas", modelo.CantidadPersonas);
                comandoInsertar.Parameters.AddWithValue("@anticipo", modelo.Anticipo);
                comandoInsertar.Parameters.AddWithValue("@saldo_pendiente", saldoPendiente);
                comandoInsertar.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(modelo.Observaciones)
                    ? DBNull.Value
                    : modelo.Observaciones.Trim());

                comandoInsertar.ExecuteNonQuery();

                TempData["Exito"] = "Reserva creada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar la reserva: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // CARGAR COMBOS
        // ===============================
        private void CargarCombos()
        {
            List<dynamic> clientes = new List<dynamic>();
            List<dynamic> habitaciones = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);
                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                // ===============================
                // CLIENTES ACTIVOS
                // ===============================
                string consultaClientes = @"
                    SELECT id_clipro, nombre
                    FROM clipro
                    WHERE id_tipoclipro = @id_tipoclipro
                      AND estado = 'activo'
                    ORDER BY nombre;";

                using (var comandoClientes = new MySqlCommand(consultaClientes, conexion))
                {
                    comandoClientes.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                    using var lectorClientes = comandoClientes.ExecuteReader();

                    while (lectorClientes.Read())
                    {
                        clientes.Add(new
                        {
                            Id = Convert.ToInt32(lectorClientes["id_clipro"]),
                            Nombre = lectorClientes["nombre"]?.ToString() ?? ""
                        });
                    }
                }

                // ===============================
                // HABITACIONES
                // ===============================
                string consultaHabitaciones = @"
                    SELECT p.id_proser, p.codigo
                    FROM proser p
                    WHERE p.id_tipoproser = @id_tipoproser
                    ORDER BY p.codigo;";

                using (var comandoHabitaciones = new MySqlCommand(consultaHabitaciones, conexion))
                {
                    comandoHabitaciones.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                    using var lectorHabitaciones = comandoHabitaciones.ExecuteReader();

                    while (lectorHabitaciones.Read())
                    {
                        habitaciones.Add(new
                        {
                            Id = Convert.ToInt32(lectorHabitaciones["id_proser"]),
                            Codigo = lectorHabitaciones["codigo"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch
            {
                // Si ocurre un error cargando combos,
                // se devolverán listas vacías y la vista mostrará el comportamiento normal.
            }

            ViewBag.Clientes = clientes;
            ViewBag.Habitaciones = habitaciones;
        }
    }
}