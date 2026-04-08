using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Services;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class ReservasController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly DisponibilidadService _disponibilidadService;
        private const int RegistrosPorPagina = 20;

        public ReservasController(
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

        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            return null;
        }

        private int ObtenerIdUsuarioSesion()
        {
            string? idUsuarioSesion = HttpContext.Session.GetString("IdUsuario");

            if (!int.TryParse(idUsuarioSesion, out int idUsuario))
            {
                throw new Exception("No se pudo identificar el usuario de la sesión.");
            }

            return idUsuario;
        }

        private string ObtenerNombreRolSesion()
        {
            return HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
        }

        private bool EsClienteSesion()
        {
            return ObtenerNombreRolSesion() == "cliente";
        }

        private int ObtenerIdClienteSesion()
        {
            if (!EsClienteSesion())
            {
                throw new Exception("La sesión actual no corresponde a un cliente.");
            }

            return ObtenerIdUsuarioSesion();
        }

        private string ObtenerNombreUsuarioSesion()
        {
            return HttpContext.Session.GetString("NombreUsuario")?.Trim() ?? "";
        }

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

        private int ObtenerIdTipoMovimiento(MySqlConnection conexion, string nombreTipo)
        {
            string consulta = @"
                SELECT id_tipomov
                FROM tipo_movimiento
                WHERE LOWER(nombre_tipomov) = @nombre
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@nombre", nombreTipo.Trim().ToLower());

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception($"No existe el tipo de movimiento '{nombreTipo}'.");
            }

            return Convert.ToInt32(resultado);
        }

        private int ObtenerIdFormaPago(MySqlConnection conexion, string nombreFormaPago)
        {
            string consulta = @"
                SELECT id_formapago
                FROM forma_pago
                WHERE LOWER(nombre_forma) = @nombre
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@nombre", nombreFormaPago.Trim().ToLower());

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception($"No existe la forma de pago '{nombreFormaPago}'.");
            }

            return Convert.ToInt32(resultado);
        }

        private string ObtenerColumnaOrden(string columna)
        {
            return columna.Trim().ToLower() switch
            {
                "cliente" => "c.nombre",
                "fecha_entrada" => "r.fecha_entrada",
                _ => "r.fecha_entrada"
            };
        }

        private void CargarFormasPago()
        {
            List<dynamic> formasPago = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT id_formapago, nombre_forma
                    FROM forma_pago
                    ORDER BY nombre_forma;";

                using var comando = new MySqlCommand(consulta, conexion);
                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    formasPago.Add(new
                    {
                        IdFormaPago = Convert.ToInt32(lector["id_formapago"]),
                        NombreForma = lector["nombre_forma"]?.ToString() ?? ""
                    });
                }
            }
            catch
            {
            }

            ViewBag.FormasPago = formasPago;
        }

        private void CargarCombos(
            DateTime? fechaEntrada = null,
            DateTime? fechaSalida = null,
            int? idHabitacionSeleccionada = null)
        {
            List<dynamic> clientes = new List<dynamic>();
            List<dynamic> habitaciones = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);
                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

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

                if (fechaEntrada.HasValue && fechaSalida.HasValue && fechaSalida.Value.Date > fechaEntrada.Value.Date)
                {
                    var habitacionesDisponibles = _disponibilidadService.ObtenerHabitacionesDisponibles(
                        fechaEntrada.Value,
                        fechaSalida.Value
                    );

                    foreach (var habitacion in habitacionesDisponibles)
                    {
                        habitaciones.Add(new
                        {
                            Id = habitacion.IdHabitacion,
                            Codigo = habitacion.NumeroHabitacion,
                            Precio = habitacion.Precio,
                            Tipo = habitacion.TipoHabitacion
                        });
                    }

                    if (idHabitacionSeleccionada.HasValue &&
                        !habitaciones.Any(h => h.Id == idHabitacionSeleccionada.Value))
                    {
                        string consultaHabitacionSeleccionada = @"
                            SELECT
                                p.id_proser,
                                p.codigo,
                                p.precio,
                                COALESCE(s.nombre_subcategoria, '-') AS tipo_habitacion
                            FROM proser p
                            LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                            WHERE p.id_proser = @id_habitacion
                              AND p.id_tipoproser = @id_tipoproser
                            LIMIT 1;";

                        using var comandoHabitacionSeleccionada = new MySqlCommand(consultaHabitacionSeleccionada, conexion);
                        comandoHabitacionSeleccionada.Parameters.AddWithValue("@id_habitacion", idHabitacionSeleccionada.Value);
                        comandoHabitacionSeleccionada.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                        using var lectorHabitacionSeleccionada = comandoHabitacionSeleccionada.ExecuteReader();

                        if (lectorHabitacionSeleccionada.Read())
                        {
                            habitaciones.Add(new
                            {
                                Id = Convert.ToInt32(lectorHabitacionSeleccionada["id_proser"]),
                                Codigo = lectorHabitacionSeleccionada["codigo"]?.ToString() ?? "",
                                Precio = Convert.ToDecimal(lectorHabitacionSeleccionada["precio"]),
                                Tipo = lectorHabitacionSeleccionada["tipo_habitacion"]?.ToString() ?? "-"
                            });
                        }
                    }
                }
                else
                {
                    string consultaHabitaciones = @"
                        SELECT 
                            p.id_proser,
                            p.codigo,
                            p.precio,
                            COALESCE(s.nombre_subcategoria, '-') AS tipo_habitacion
                        FROM proser p
                        LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                        WHERE p.id_tipoproser = @id_tipoproser
                        ORDER BY p.codigo;";

                    using var comandoHabitaciones = new MySqlCommand(consultaHabitaciones, conexion);
                    comandoHabitaciones.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                    using var lectorHabitaciones = comandoHabitaciones.ExecuteReader();

                    while (lectorHabitaciones.Read())
                    {
                        habitaciones.Add(new
                        {
                            Id = Convert.ToInt32(lectorHabitaciones["id_proser"]),
                            Codigo = lectorHabitaciones["codigo"]?.ToString() ?? "",
                            Precio = Convert.ToDecimal(lectorHabitaciones["precio"]),
                            Tipo = lectorHabitaciones["tipo_habitacion"]?.ToString() ?? "-"
                        });
                    }
                }
            }
            catch
            {
            }

            ViewBag.Clientes = clientes;
            ViewBag.Habitaciones = habitaciones;
        }

        private decimal ObtenerTotalPagadoReserva(MySqlConnection conexion, int idReserva)
        {
            string consulta = @"
                SELECT COALESCE(SUM(d.subtotal), 0)
                FROM movimiento m
                INNER JOIN detalle d ON m.id_movimiento = d.id_movimiento
                INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                WHERE m.id_reserva = @id_reserva
                  AND m.estado = 'activo'
                  AND LOWER(tm.nombre_tipomov) = 'reserva';";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_reserva", idReserva);

            object? resultado = comando.ExecuteScalar();
            return resultado == null || resultado == DBNull.Value ? 0 : Convert.ToDecimal(resultado);
        }

        private void RegistrarMovimientoReserva(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idReserva,
            int idUsuario,
            int idClipro,
            int idHabitacion,
            int idFormaPago,
            int idTipoMovimiento,
            decimal monto,
            string descripcionDetalle,
            string? observaciones)
        {
            string insertarMovimiento = @"
                INSERT INTO movimiento
                (
                    id_usuario,
                    id_clipro,
                    id_tipomov,
                    id_formapago,
                    id_reserva,
                    estado,
                    observaciones
                )
                VALUES
                (
                    @id_usuario,
                    @id_clipro,
                    @id_tipomov,
                    @id_formapago,
                    @id_reserva,
                    'activo',
                    @observaciones
                );";

            int idMovimientoGenerado;

            using (var comandoMovimiento = new MySqlCommand(insertarMovimiento, conexion, transaccion))
            {
                comandoMovimiento.Parameters.AddWithValue("@id_usuario", idUsuario);
                comandoMovimiento.Parameters.AddWithValue("@id_clipro", idClipro);
                comandoMovimiento.Parameters.AddWithValue("@id_tipomov", idTipoMovimiento);
                comandoMovimiento.Parameters.AddWithValue("@id_formapago", idFormaPago);
                comandoMovimiento.Parameters.AddWithValue("@id_reserva", idReserva);
                comandoMovimiento.Parameters.AddWithValue("@observaciones",
                    string.IsNullOrWhiteSpace(observaciones) ? DBNull.Value : observaciones.Trim());

                comandoMovimiento.ExecuteNonQuery();
                idMovimientoGenerado = Convert.ToInt32(comandoMovimiento.LastInsertedId);
            }

            string insertarDetalle = @"
                INSERT INTO detalle
                (
                    id_movimiento,
                    id_proser,
                    cantidad,
                    precio_unitario,
                    subtotal,
                    descripcion
                )
                VALUES
                (
                    @id_movimiento,
                    @id_proser,
                    1,
                    @precio_unitario,
                    @subtotal,
                    @descripcion
                );";

            using var comandoDetalle = new MySqlCommand(insertarDetalle, conexion, transaccion);
            comandoDetalle.Parameters.AddWithValue("@id_movimiento", idMovimientoGenerado);
            comandoDetalle.Parameters.AddWithValue("@id_proser", idHabitacion);
            comandoDetalle.Parameters.AddWithValue("@precio_unitario", monto);
            comandoDetalle.Parameters.AddWithValue("@subtotal", monto);
            comandoDetalle.Parameters.AddWithValue("@descripcion", descripcionDetalle);

            comandoDetalle.ExecuteNonQuery();
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
                string[] estadosPermitidos = { "todas", "pendiente", "confirmada", "en_curso", "cancelada", "finalizada" };

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

                string condicionEstado = vistaNormalizada == "todas" ? "" : "AND r.estado = @estado";
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
            WHERE 1 = 1
            {condicionEstado}
            {condicionBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);

                if (vistaNormalizada != "todas")
                {
                    comandoConteo.Parameters.AddWithValue("@estado", vistaNormalizada);
                }

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
                r.total_reserva,
                r.saldo_pendiente,
                r.estado,
                r.observaciones
            FROM reserva r
            INNER JOIN clipro c ON r.id_clipro = c.id_clipro
            INNER JOIN proser p ON r.id_habitacion = p.id_proser
            WHERE 1 = 1
            {condicionEstado}
            {condicionBusqueda}
            ORDER BY {columnaOrden} {direccionOrden}
            LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);

                if (vistaNormalizada != "todas")
                {
                    comando.Parameters.AddWithValue("@estado", vistaNormalizada);
                }

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
        // DETALLE DE RESERVA
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            ReservaDetalleViewModel modelo = new ReservaDetalleViewModel();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        r.id_reserva,
                        c.nombre AS cliente,
                        p.codigo AS habitacion,
                        r.fecha_entrada,
                        r.fecha_salida,
                        r.cantidad_personas,
                        r.total_reserva,
                        r.saldo_pendiente,
                        r.estado,
                        r.observaciones
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    WHERE r.id_reserva = @id
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                    return RedirectToAction("Index");
                }

                modelo = new ReservaDetalleViewModel
                {
                    IdReserva = Convert.ToInt32(lector["id_reserva"]),
                    Cliente = lector["cliente"]?.ToString() ?? "",
                    Habitacion = lector["habitacion"]?.ToString() ?? "",
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    CantidadPersonas = Convert.ToInt32(lector["cantidad_personas"]),
                    TotalReserva = Convert.ToDecimal(lector["total_reserva"]),
                    SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]),
                    Estado = lector["estado"]?.ToString() ?? "",
                    Observaciones = lector["observaciones"] == DBNull.Value
                        ? null
                        : lector["observaciones"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cargar el detalle de la reserva: " + ex.Message;
                return RedirectToAction("Index");
            }

            try
            {
                using var conexionPagos = _conexionBD.ObtenerConexion();
                conexionPagos.Open();
                modelo.TotalPagado = ObtenerTotalPagadoReserva(conexionPagos, id);
            }
            catch
            {
                modelo.TotalPagado = 0;
            }

            return View(modelo);
        }

        // ===============================
        // CREAR RESERVA - VISTA
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(
            int? idHabitacion = null,
            int? idClipro = null,
            DateTime? fechaEntrada = null,
            DateTime? fechaSalida = null,
            int? cantidadPersonas = null,
            decimal? montoPagoInicial = null,
            int? idFormaPagoInicial = null,
            string? observaciones = null)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            bool esCliente = EsClienteSesion();

            CargarCombos(fechaEntrada, fechaSalida, idHabitacion);
            CargarFormasPago();

            ReservaFormViewModel modelo = new ReservaFormViewModel();

            if (fechaEntrada.HasValue)
            {
                modelo.FechaEntrada = fechaEntrada.Value;
            }

            if (fechaSalida.HasValue)
            {
                modelo.FechaSalida = fechaSalida.Value;
            }

            if (idHabitacion.HasValue)
            {
                modelo.IdHabitacion = idHabitacion.Value;
            }

            if (cantidadPersonas.HasValue)
            {
                modelo.CantidadPersonas = cantidadPersonas.Value;
            }

            if (montoPagoInicial.HasValue)
            {
                modelo.MontoPagoInicial = montoPagoInicial.Value;
            }

            if (idFormaPagoInicial.HasValue)
            {
                modelo.IdFormaPagoInicial = idFormaPagoInicial.Value;
            }

            if (!string.IsNullOrWhiteSpace(observaciones))
            {
                modelo.Observaciones = observaciones;
            }

            if (esCliente)
            {
                modelo.IdClipro = ObtenerIdClienteSesion();
                ViewBag.NombreClienteSesion = ObtenerNombreUsuarioSesion();
            }
            else if (idClipro.HasValue)
            {
                modelo.IdClipro = idClipro.Value;
            }

            ViewBag.EsClienteSesion = esCliente;
            ViewBag.BloquearHabitacion = idHabitacion.HasValue;

            return View(modelo);
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

            bool esCliente = EsClienteSesion();

            if (esCliente)
            {
                modelo.IdClipro = ObtenerIdClienteSesion();
                ViewBag.NombreClienteSesion = ObtenerNombreUsuarioSesion();
            }

            ViewBag.EsClienteSesion = esCliente;
            ViewBag.BloquearHabitacion = modelo.IdHabitacion > 0;

            CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
            CargarFormasPago();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

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

                if (modelo.MontoPagoInicial < 0)
                {
                    ModelState.AddModelError("", "El pago inicial no puede ser negativo.");
                    return View(modelo);
                }

                if (modelo.MontoPagoInicial > 0 && !modelo.IdFormaPagoInicial.HasValue)
                {
                    ModelState.AddModelError("", "Debe seleccionar una forma de pago cuando ingrese un monto inicial.");
                    return View(modelo);
                }

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

                bool habitacionDisponible = _disponibilidadService.EstaHabitacionDisponible(
                    modelo.IdHabitacion,
                    modelo.FechaEntrada,
                    modelo.FechaSalida
                );

                if (!habitacionDisponible)
                {
                    ModelState.AddModelError("", "La habitación ya no está disponible en esas fechas.");
                    CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
                    return View(modelo);
                }

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

                if (modelo.MontoPagoInicial > totalReserva)
                {
                    ModelState.AddModelError("", "El pago inicial no puede ser mayor al total de la reserva.");
                    return View(modelo);
                }

                decimal saldoPendiente = totalReserva - modelo.MontoPagoInicial;

                using var transaccion = conexion.BeginTransaction();

                string insertarReserva = @"
                    INSERT INTO reserva
                    (
                        id_clipro,
                        id_habitacion,
                        fecha_entrada,
                        fecha_salida,
                        cantidad_personas,
                        total_reserva,
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
                        @total_reserva,
                        @saldo_pendiente,
                        'pendiente',
                        @observaciones
                    );";

                int idReservaGenerada;

                using (var comandoInsertar = new MySqlCommand(insertarReserva, conexion, transaccion))
                {
                    comandoInsertar.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                    comandoInsertar.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                    comandoInsertar.Parameters.AddWithValue("@fecha_entrada", modelo.FechaEntrada.Date);
                    comandoInsertar.Parameters.AddWithValue("@fecha_salida", modelo.FechaSalida.Date);
                    comandoInsertar.Parameters.AddWithValue("@cantidad_personas", modelo.CantidadPersonas);
                    comandoInsertar.Parameters.AddWithValue("@total_reserva", totalReserva);
                    comandoInsertar.Parameters.AddWithValue("@saldo_pendiente", saldoPendiente);
                    comandoInsertar.Parameters.AddWithValue("@observaciones",
                        string.IsNullOrWhiteSpace(modelo.Observaciones)
                            ? DBNull.Value
                            : modelo.Observaciones.Trim());

                    comandoInsertar.ExecuteNonQuery();
                    idReservaGenerada = Convert.ToInt32(comandoInsertar.LastInsertedId);
                }

                int idUsuario = ObtenerIdUsuarioSesion();
                int idTipoMovimientoReserva = ObtenerIdTipoMovimiento(conexion, "reserva");
                int idTipoMovimientoCxc = ObtenerIdTipoMovimiento(conexion, "cuenta_por_cobrar");

                if (modelo.MontoPagoInicial > 0)
                {
                    RegistrarMovimientoReserva(
                        conexion,
                        transaccion,
                        idReservaGenerada,
                        idUsuario,
                        modelo.IdClipro,
                        modelo.IdHabitacion,
                        modelo.IdFormaPagoInicial!.Value,
                        idTipoMovimientoReserva,
                        modelo.MontoPagoInicial,
                        $"Pago inicial de reserva #{idReservaGenerada}",
                        modelo.Observaciones
                    );
                }

                if (saldoPendiente > 0)
                {
                    int idFormaPagoCredito = ObtenerIdFormaPago(conexion, "credito");

                    RegistrarMovimientoReserva(
                        conexion,
                        transaccion,
                        idReservaGenerada,
                        idUsuario,
                        modelo.IdClipro,
                        modelo.IdHabitacion,
                        idFormaPagoCredito,
                        idTipoMovimientoCxc,
                        saldoPendiente,
                        $"Cuenta por cobrar generada para reserva #{idReservaGenerada}",
                        modelo.Observaciones
                    );
                }

                transaccion.Commit();

                TempData["Exito"] = "Reserva creada correctamente.";

                if (esCliente)
                {
                    return RedirectToAction("Index", "Panel");
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar la reserva: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // EDITAR RESERVA - VISTA
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        id_reserva,
                        id_clipro,
                        id_habitacion,
                        fecha_entrada,
                        fecha_salida,
                        cantidad_personas,
                        observaciones,
                        estado,
                        total_reserva
                    FROM reserva
                    WHERE id_reserva = @id
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                    return RedirectToAction("Index");
                }

                string estado = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";

                if (estado != "pendiente" && estado != "confirmada")
                {
                    TempData["Mensaje"] = "Solo se pueden editar reservas pendientes o confirmadas.";
                    return RedirectToAction("Index");
                }

                ReservaFormViewModel modelo = new ReservaFormViewModel
                {
                    IdReserva = Convert.ToInt32(lector["id_reserva"]),
                    IdClipro = Convert.ToInt32(lector["id_clipro"]),
                    IdHabitacion = Convert.ToInt32(lector["id_habitacion"]),
                    FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                    FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                    CantidadPersonas = Convert.ToInt32(lector["cantidad_personas"]),
                    Observaciones = lector["observaciones"] == DBNull.Value
                        ? null
                        : lector["observaciones"]?.ToString(),
                    TotalReserva = Convert.ToDecimal(lector["total_reserva"])
                };

                CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar la reserva: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ===============================
        // EDITAR RESERVA - GUARDAR
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(ReservaFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consultaReservaActual = @"
                    SELECT id_reserva, estado
                    FROM reserva
                    WHERE id_reserva = @id_reserva
                    LIMIT 1;";

                using (var comandoEstado = new MySqlCommand(consultaReservaActual, conexion))
                {
                    comandoEstado.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);

                    using var lector = comandoEstado.ExecuteReader();

                    if (!lector.Read())
                    {
                        TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                        return RedirectToAction("Index");
                    }

                    string estadoActual = lector["estado"]?.ToString()?.Trim().ToLower() ?? "";

                    if (estadoActual != "pendiente" && estadoActual != "confirmada")
                    {
                        TempData["Mensaje"] = "Solo se pueden editar reservas pendientes o confirmadas.";
                        return RedirectToAction("Index");
                    }
                }

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

                bool habitacionDisponible = _disponibilidadService.EstaHabitacionDisponible(
                    modelo.IdHabitacion,
                    modelo.FechaEntrada,
                    modelo.FechaSalida,
                    modelo.IdReserva
                );

                if (!habitacionDisponible)
                {
                    ModelState.AddModelError("", "La habitación ya no está disponible en esas fechas.");
                    CargarCombos(modelo.FechaEntrada, modelo.FechaSalida, modelo.IdHabitacion);
                    return View(modelo);
                }

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
                decimal totalPagado = ObtenerTotalPagadoReserva(conexion, modelo.IdReserva);
                decimal saldoPendiente = totalReserva - totalPagado;

                if (saldoPendiente < 0)
                {
                    ModelState.AddModelError("", "Los pagos ya registrados superan el nuevo total de la reserva.");
                    return View(modelo);
                }

                string actualizar = @"
                    UPDATE reserva
                    SET id_clipro = @id_clipro,
                        id_habitacion = @id_habitacion,
                        fecha_entrada = @fecha_entrada,
                        fecha_salida = @fecha_salida,
                        cantidad_personas = @cantidad_personas,
                        total_reserva = @total_reserva,
                        saldo_pendiente = @saldo_pendiente,
                        observaciones = @observaciones
                    WHERE id_reserva = @id_reserva;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                comandoActualizar.Parameters.AddWithValue("@id_habitacion", modelo.IdHabitacion);
                comandoActualizar.Parameters.AddWithValue("@fecha_entrada", modelo.FechaEntrada.Date);
                comandoActualizar.Parameters.AddWithValue("@fecha_salida", modelo.FechaSalida.Date);
                comandoActualizar.Parameters.AddWithValue("@cantidad_personas", modelo.CantidadPersonas);
                comandoActualizar.Parameters.AddWithValue("@total_reserva", totalReserva);
                comandoActualizar.Parameters.AddWithValue("@saldo_pendiente", saldoPendiente);
                comandoActualizar.Parameters.AddWithValue("@observaciones",
                    string.IsNullOrWhiteSpace(modelo.Observaciones)
                        ? DBNull.Value
                        : modelo.Observaciones.Trim());
                comandoActualizar.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Reserva actualizada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar la reserva: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // PAGOS DE RESERVA - VISTA
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Pagos(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarFormasPago();
            CargarDatosPagoReserva(id);

            ReservaPagoViewModel modelo = new ReservaPagoViewModel
            {
                IdReserva = id
            };

            return View(modelo);
        }

        // ===============================
        // REGISTRAR PAGO DE RESERVA
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult RegistrarPago(ReservaPagoViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarFormasPago();

            if (!ModelState.IsValid)
            {
                CargarDatosPagoReserva(modelo.IdReserva);
                return View("Pagos", modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var transaccion = conexion.BeginTransaction();

                string consultaReserva = @"
                    SELECT
                        id_reserva,
                        id_clipro,
                        id_habitacion,
                        total_reserva,
                        saldo_pendiente,
                        estado
                    FROM reserva
                    WHERE id_reserva = @id_reserva
                    LIMIT 1;";

                int idClipro;
                int idHabitacion;
                decimal totalReserva;
                decimal saldoActual;
                string estadoReserva;

                using (var comandoReserva = new MySqlCommand(consultaReserva, conexion, transaccion))
                {
                    comandoReserva.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);

                    using var lectorReserva = comandoReserva.ExecuteReader();

                    if (!lectorReserva.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reserva solicitada.";
                        return RedirectToAction("Index");
                    }

                    idClipro = Convert.ToInt32(lectorReserva["id_clipro"]);
                    idHabitacion = Convert.ToInt32(lectorReserva["id_habitacion"]);
                    totalReserva = Convert.ToDecimal(lectorReserva["total_reserva"]);
                    saldoActual = Convert.ToDecimal(lectorReserva["saldo_pendiente"]);
                    estadoReserva = lectorReserva["estado"]?.ToString()?.Trim().ToLower() ?? "";
                }

                if (estadoReserva != "pendiente" && estadoReserva != "confirmada")
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Solo se pueden registrar pagos en reservas pendientes o confirmadas.";
                    return RedirectToAction("Pagos", new { id = modelo.IdReserva });
                }

                if (modelo.Monto > saldoActual)
                {
                    transaccion.Rollback();
                    ModelState.AddModelError("", "El monto ingresado no puede ser mayor al saldo pendiente.");
                    CargarDatosPagoReserva(modelo.IdReserva);
                    return View("Pagos", modelo);
                }

                int idUsuario = ObtenerIdUsuarioSesion();
                int idTipoMovimientoReserva = ObtenerIdTipoMovimiento(conexion, "reserva");

                RegistrarMovimientoReserva(
                    conexion,
                    transaccion,
                    modelo.IdReserva,
                    idUsuario,
                    idClipro,
                    idHabitacion,
                    modelo.IdFormaPago,
                    idTipoMovimientoReserva,
                    modelo.Monto,
                    $"Pago registrado a reserva #{modelo.IdReserva}",
                    modelo.Observaciones
                );

                decimal totalPagadoNuevo = ObtenerTotalPagadoReserva(conexion, modelo.IdReserva);
                decimal nuevoSaldo = totalReserva - totalPagadoNuevo;

                if (nuevoSaldo < 0)
                {
                    transaccion.Rollback();
                    ModelState.AddModelError("", "La suma de pagos supera el total de la reserva.");
                    CargarDatosPagoReserva(modelo.IdReserva);
                    return View("Pagos", modelo);
                }

                string actualizarReserva = @"
                    UPDATE reserva
                    SET saldo_pendiente = @saldo_pendiente
                    WHERE id_reserva = @id_reserva;";

                using (var comandoActualizar = new MySqlCommand(actualizarReserva, conexion, transaccion))
                {
                    comandoActualizar.Parameters.AddWithValue("@saldo_pendiente", nuevoSaldo);
                    comandoActualizar.Parameters.AddWithValue("@id_reserva", modelo.IdReserva);
                    comandoActualizar.ExecuteNonQuery();
                }

                transaccion.Commit();

                TempData["Exito"] = "Pago registrado correctamente.";
                return RedirectToAction("Pagos", new { id = modelo.IdReserva });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ocurrió un error al registrar el pago: " + ex.Message);
                CargarDatosPagoReserva(modelo.IdReserva);
                return View("Pagos", modelo);
            }
        }

        private void CargarDatosPagoReserva(int idReserva)
        {
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consultaReserva = @"
                    SELECT
                        r.id_reserva,
                        c.nombre AS cliente,
                        p.codigo AS habitacion,
                        COALESCE(s.nombre_subcategoria, '-') AS tipo_habitacion,
                        r.total_reserva,
                        r.saldo_pendiente,
                        r.estado
                    FROM reserva r
                    INNER JOIN clipro c ON r.id_clipro = c.id_clipro
                    INNER JOIN proser p ON r.id_habitacion = p.id_proser
                    LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                    WHERE r.id_reserva = @id
                    LIMIT 1;";

                using (var comandoReserva = new MySqlCommand(consultaReserva, conexion))
                {
                    comandoReserva.Parameters.AddWithValue("@id", idReserva);

                    using var lectorReserva = comandoReserva.ExecuteReader();

                    if (lectorReserva.Read())
                    {
                        ViewBag.Reserva = new
                        {
                            IdReserva = Convert.ToInt32(lectorReserva["id_reserva"]),
                            Cliente = lectorReserva["cliente"]?.ToString() ?? "",
                            Habitacion = lectorReserva["habitacion"]?.ToString() ?? "",
                            TipoHabitacion = lectorReserva["tipo_habitacion"]?.ToString() ?? "-",
                            TotalReserva = Convert.ToDecimal(lectorReserva["total_reserva"]),
                            SaldoPendiente = Convert.ToDecimal(lectorReserva["saldo_pendiente"]),
                            Estado = lectorReserva["estado"]?.ToString() ?? ""
                        };
                    }
                }

                decimal totalPagado = ObtenerTotalPagadoReserva(conexion, idReserva);
                ViewBag.TotalPagado = totalPagado;

                List<dynamic> pagos = new List<dynamic>();

                string consultaPagos = @"
                    SELECT
                        m.id_movimiento,
                        tm.nombre_tipomov,
                        fp.nombre_forma,
                        m.fecha_hora,
                        d.subtotal AS monto,
                        m.observaciones
                    FROM movimiento m
                    INNER JOIN detalle d ON m.id_movimiento = d.id_movimiento
                    INNER JOIN tipo_movimiento tm ON m.id_tipomov = tm.id_tipomov
                    INNER JOIN forma_pago fp ON m.id_formapago = fp.id_formapago
                    WHERE m.id_reserva = @id_reserva
                      AND m.estado = 'activo'
                      AND LOWER(tm.nombre_tipomov) = 'reserva'
                    ORDER BY m.fecha_hora DESC, m.id_movimiento DESC;";

                using (var comandoPagos = new MySqlCommand(consultaPagos, conexion))
                {
                    comandoPagos.Parameters.AddWithValue("@id_reserva", idReserva);

                    using var lectorPagos = comandoPagos.ExecuteReader();

                    while (lectorPagos.Read())
                    {
                        pagos.Add(new
                        {
                            IdMovimiento = Convert.ToInt32(lectorPagos["id_movimiento"]),
                            NombreForma = lectorPagos["nombre_forma"]?.ToString() ?? "",
                            FechaHora = Convert.ToDateTime(lectorPagos["fecha_hora"]),
                            Monto = Convert.ToDecimal(lectorPagos["monto"]),
                            Observaciones = lectorPagos["observaciones"] == DBNull.Value ? null : lectorPagos["observaciones"]?.ToString()
                        });
                    }
                }

                ViewBag.ListaPagos = pagos;
            }
            catch
            {
            }
        }

        // ===============================
        // CONFIRMAR RESERVA
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Confirmar(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    UPDATE reserva
                    SET estado = 'confirmada'
                    WHERE id_reserva = @id
                      AND estado = 'pendiente';";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                int filas = comando.ExecuteNonQuery();

                if (filas == 0)
                {
                    TempData["Mensaje"] = "No se pudo confirmar la reserva.";
                }
                else
                {
                    TempData["Exito"] = "Reserva confirmada correctamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al confirmar la reserva: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        private int ObtenerIdEstadoHabitacion(MySqlConnection conexion, string nombre)
        {
            string query = @"
                SELECT id_tipoestado
                FROM tipo_estado
                WHERE LOWER(estado) = @nombre
                LIMIT 1;";

            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@nombre", nombre.ToLower());

            object? result = cmd.ExecuteScalar();

            if (result == null)
                throw new Exception($"Estado '{nombre}' no existe.");

            return Convert.ToInt32(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckIn(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var transaccion = conexion.BeginTransaction();

                string consulta = @"
                    SELECT id_reserva, id_habitacion, estado
                    FROM reserva
                    WHERE id_reserva = @id
                    LIMIT 1;";

                int idHabitacion;
                string estado;

                using (var cmd = new MySqlCommand(consulta, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Reserva no encontrada.";
                        return RedirectToAction("Index");
                    }

                    estado = reader["estado"]?.ToString()?.Trim().ToLower() ?? "";
                    idHabitacion = Convert.ToInt32(reader["id_habitacion"]);
                }

                if (estado != "confirmada")
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Solo las reservas confirmadas pueden realizar check-in.";
                    return RedirectToAction("Index");
                }

                string updateReserva = @"
                    UPDATE reserva
                    SET estado = 'en_curso'
                    WHERE id_reserva = @id;";

                using (var cmd = new MySqlCommand(updateReserva, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }

                int idEstadoOcupada = ObtenerIdEstadoHabitacion(conexion, "ocupada");

                string updateHabitacion = @"
                    UPDATE proser
                    SET id_tipoestado = @estado
                    WHERE id_proser = @id;";

                using (var cmd = new MySqlCommand(updateHabitacion, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@estado", idEstadoOcupada);
                    cmd.Parameters.AddWithValue("@id", idHabitacion);
                    cmd.ExecuteNonQuery();
                }

                transaccion.Commit();

                TempData["Exito"] = "Check-in realizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al realizar el check-in: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ===============================
        // METODO CHECK OUT
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckOut(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                using var transaccion = conexion.BeginTransaction();

                string consulta = @"
                    SELECT id_reserva, id_habitacion, estado
                    FROM reserva
                    WHERE id_reserva = @id
                    LIMIT 1;";

                int idHabitacion;
                string estado;

                using (var cmd = new MySqlCommand(consulta, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Reserva no encontrada.";
                        return RedirectToAction("Index");
                    }

                    estado = reader["estado"]?.ToString()?.Trim().ToLower() ?? "";
                    idHabitacion = Convert.ToInt32(reader["id_habitacion"]);
                }

                if (estado != "en_curso")
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Solo las reservas en curso pueden realizar check-out.";
                    return RedirectToAction("Index");
                }

                string updateReserva = @"
                    UPDATE reserva
                    SET estado = 'finalizada'
                    WHERE id_reserva = @id;";

                using (var cmd = new MySqlCommand(updateReserva, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }

                int idEstadoLibre = ObtenerIdEstadoHabitacion(conexion, "libre");

                string updateHabitacion = @"
                    UPDATE proser
                    SET id_tipoestado = @estado
                    WHERE id_proser = @id;";

                using (var cmd = new MySqlCommand(updateHabitacion, conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@estado", idEstadoLibre);
                    cmd.Parameters.AddWithValue("@id", idHabitacion);
                    cmd.ExecuteNonQuery();
                }

                transaccion.Commit();

                TempData["Exito"] = "Check-out realizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al realizar el check-out: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ===============================
        // CANCELAR RESERVA
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Cancelar(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    UPDATE reserva
                    SET estado = 'cancelada'
                    WHERE id_reserva = @id
                      AND estado IN ('pendiente', 'confirmada');";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                int filas = comando.ExecuteNonQuery();

                if (filas == 0)
                {
                    TempData["Mensaje"] = "No se pudo cancelar la reserva.";
                }
                else
                {
                    TempData["Exito"] = "Reserva cancelada correctamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al cancelar la reserva: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}