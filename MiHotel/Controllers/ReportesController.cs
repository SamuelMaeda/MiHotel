using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class ReportesController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public ReportesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarAcceso(out bool esAdministrador)
        {
            esAdministrador = false;
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("IdUsuario")))
                return RedirectToAction("Login", "Acceso");

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
            esAdministrador = rol == "admin";

            if (!esAdministrador && rol != "recepcionista")
            {
                TempData["Mensaje"] = "No tiene acceso al módulo de reportes.";
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(DateTime? fechaInicio, DateTime? fechaFin)
        {
            IActionResult? acceso = ValidarAcceso(out bool esAdministrador);
            if (acceso != null) return acceso;

            DateTime hoy = DateTime.Today;
            DateTime inicio = (fechaInicio ?? new DateTime(hoy.Year, hoy.Month, 1)).Date;
            DateTime fin = (fechaFin ?? hoy).Date;

            if (fin < inicio)
            {
                ModelState.AddModelError(string.Empty, "La fecha final no puede ser anterior a la fecha inicial.");
                fin = inicio;
            }

            if ((fin - inicio).TotalDays > 3660)
            {
                ModelState.AddModelError(string.Empty, "El período del reporte no puede superar diez años.");
                fin = inicio.AddYears(10);
            }

            var modelo = new ReporteGeneralViewModel
            {
                FechaInicio = inicio,
                FechaFin = fin,
                EsAdministrador = esAdministrador
            };

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                DateTime finExclusiva = fin.AddDays(1);
                int diasPeriodo = (fin - inicio).Days + 1;

                modelo.HabitacionesActivas = ObtenerEntero(conexion, @"
                    SELECT COUNT(*)
                    FROM proser p
                    INNER JOIN tipo_proser tp ON tp.id_tipoproser=p.id_tipoproser
                    INNER JOIN tipo_estado te ON te.id_tipoestado=p.id_tipoestado
                    WHERE LOWER(tp.nombre)='habitacion'
                      AND LOWER(te.estado) NOT IN ('inactivo','remodelacion');");

                modelo.NochesDisponibles = modelo.HabitacionesActivas * diasPeriodo;

                const string filtroReservas = @"
                    r.fecha_entrada < @fin_exclusiva
                    AND r.fecha_salida > @inicio";

                using (var comando = new MySqlCommand($@"
                    SELECT
                        COUNT(*) AS total_reservas,
                        COALESCE(SUM(CASE WHEN r.estado='finalizada' THEN 1 ELSE 0 END),0) AS finalizadas,
                        COALESCE(SUM(CASE WHEN r.estado='cancelada' THEN 1 ELSE 0 END),0) AS canceladas,
                        COALESCE(SUM(CASE WHEN r.estado<>'cancelada' THEN r.cantidad_personas ELSE 0 END),0) AS personas,
                        COALESCE(SUM(CASE WHEN r.estado<>'cancelada' THEN
                            GREATEST(0,DATEDIFF(LEAST(r.fecha_salida,@fin_exclusiva),GREATEST(r.fecha_entrada,@inicio)))
                            ELSE 0 END),0) AS noches,
                        COALESCE(SUM(CASE WHEN r.estado<>'cancelada' THEN
                            (r.total_reserva / GREATEST(1,DATEDIFF(r.fecha_salida,r.fecha_entrada))) *
                            GREATEST(0,DATEDIFF(LEAST(r.fecha_salida,@fin_exclusiva),GREATEST(r.fecha_entrada,@inicio)))
                            ELSE 0 END),0) AS hospedaje,
                        COALESCE(SUM(CASE WHEN r.estado<>'cancelada' THEN r.saldo_pendiente ELSE 0 END),0) AS saldo
                    FROM reserva r
                    WHERE {filtroReservas};", conexion))
                {
                    AgregarFechas(comando, inicio, finExclusiva);
                    using var lector = comando.ExecuteReader();
                    if (lector.Read())
                    {
                        modelo.TotalReservas = Convert.ToInt32(lector["total_reservas"]);
                        modelo.ReservasFinalizadas = Convert.ToInt32(lector["finalizadas"]);
                        modelo.ReservasCanceladas = Convert.ToInt32(lector["canceladas"]);
                        modelo.PersonasRegistradas = Convert.ToInt32(lector["personas"]);
                        modelo.NochesReservadas = Convert.ToInt32(lector["noches"]);
                        modelo.IngresosHospedaje = Convert.ToDecimal(lector["hospedaje"]);
                        modelo.SaldoPendiente = Convert.ToDecimal(lector["saldo"]);
                    }
                }

                modelo.PorcentajeOcupacion = modelo.NochesDisponibles == 0
                    ? 0
                    : Math.Min(100m, modelo.NochesReservadas * 100m / modelo.NochesDisponibles);

                using (var comando = new MySqlCommand($@"
                    SELECT r.estado,COUNT(*) AS cantidad
                    FROM reserva r
                    WHERE {filtroReservas}
                    GROUP BY r.estado
                    ORDER BY cantidad DESC,r.estado;", conexion))
                {
                    AgregarFechas(comando, inicio, finExclusiva);
                    using var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        modelo.ReservasPorEstado.Add(new ReporteEstadoReservaViewModel
                        {
                            Estado = lector["estado"]?.ToString() ?? "",
                            Cantidad = Convert.ToInt32(lector["cantidad"])
                        });
                    }
                }

                using (var comando = new MySqlCommand($@"
                    SELECT p.nombre_proser AS habitacion,
                        COALESCE(SUM(GREATEST(0,DATEDIFF(
                            LEAST(r.fecha_salida,@fin_exclusiva),
                            GREATEST(r.fecha_entrada,@inicio)))),0) AS noches
                    FROM proser p
                    INNER JOIN tipo_proser tp ON tp.id_tipoproser=p.id_tipoproser
                    LEFT JOIN reserva r ON r.id_habitacion=p.id_proser
                        AND r.estado<>'cancelada'
                        AND {filtroReservas}
                    WHERE LOWER(tp.nombre)='habitacion'
                    GROUP BY p.id_proser,p.nombre_proser
                    HAVING noches>0
                    ORDER BY noches DESC,p.nombre_proser
                    LIMIT 10;", conexion))
                {
                    AgregarFechas(comando, inicio, finExclusiva);
                    using var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        int noches = Convert.ToInt32(lector["noches"]);
                        modelo.HabitacionesMasUtilizadas.Add(new ReporteHabitacionViewModel
                        {
                            Habitacion = lector["habitacion"]?.ToString() ?? "",
                            NochesReservadas = noches,
                            PorcentajeUso = diasPeriodo == 0 ? 0 : Math.Min(100m, noches * 100m / diasPeriodo)
                        });
                    }
                }

                if (esAdministrador)
                {
                    using (var comandoClientes = new MySqlCommand($@"
                        SELECT
                            c.nombre AS cliente,
                            COALESCE(SUM(GREATEST(0,DATEDIFF(
                                LEAST(r.fecha_salida,@fin_exclusiva),
                                GREATEST(r.fecha_entrada,@inicio)))),0) AS noches,
                            MAX(r.fecha_entrada) AS ultima_entrada
                        FROM reserva r
                        INNER JOIN clipro c ON c.id_clipro=r.id_clipro
                        WHERE r.estado<>'cancelada'
                          AND {filtroReservas}
                        GROUP BY c.id_clipro,c.nombre
                        ORDER BY noches DESC,ultima_entrada DESC
                        LIMIT 10;", conexion))
                    {
                        AgregarFechas(comandoClientes, inicio, finExclusiva);
                        using var lectorClientes = comandoClientes.ExecuteReader();
                        while (lectorClientes.Read())
                        {
                            modelo.ClientesFrecuentes.Add(new ReporteClienteFrecuenteViewModel
                            {
                                Cliente = lectorClientes["cliente"]?.ToString() ?? "",
                                NochesReservadas = Convert.ToInt32(lectorClientes["noches"]),
                                UltimaEntrada = Convert.ToDateTime(lectorClientes["ultima_entrada"])
                            });
                        }
                    }

                    using (var comandoEmpresas = new MySqlCommand($@"
                        SELECT
                            e.nombre AS empresa,
                            COUNT(DISTINCT r.id_clipro) AS clientes,
                            COALESCE(SUM(GREATEST(0,DATEDIFF(
                                LEAST(r.fecha_salida,@fin_exclusiva),
                                GREATEST(r.fecha_entrada,@inicio)))),0) AS noches
                        FROM reserva r
                        INNER JOIN cliente_detalle cd ON cd.id_clipro=r.id_clipro
                        INNER JOIN empresa_cliente e ON e.id_empresa_cliente=cd.id_empresa_cliente
                        WHERE r.estado<>'cancelada'
                          AND {filtroReservas}
                        GROUP BY e.id_empresa_cliente,e.nombre
                        ORDER BY noches DESC,clientes DESC,e.nombre
                        LIMIT 10;", conexion))
                    {
                        AgregarFechas(comandoEmpresas, inicio, finExclusiva);
                        using var lectorEmpresas = comandoEmpresas.ExecuteReader();
                        while (lectorEmpresas.Read())
                        {
                            modelo.EmpresasFrecuentes.Add(new ReporteEmpresaFrecuenteViewModel
                            {
                                Empresa = lectorEmpresas["empresa"]?.ToString() ?? "",
                                Clientes = Convert.ToInt32(lectorEmpresas["clientes"]),
                                NochesReservadas = Convert.ToInt32(lectorEmpresas["noches"])
                            });
                        }
                    }

                    using (var comandoFechas = new MySqlCommand(@"
                        SELECT
                            r.fecha_entrada AS fecha,
                            COUNT(*) AS llegadas,
                            COALESCE(SUM(r.cantidad_personas),0) AS personas
                        FROM reserva r
                        WHERE r.estado<>'cancelada'
                          AND r.fecha_entrada>=@inicio
                          AND r.fecha_entrada<@fin_exclusiva
                        GROUP BY r.fecha_entrada
                        ORDER BY llegadas DESC,personas DESC,r.fecha_entrada DESC
                        LIMIT 7;", conexion))
                    {
                        AgregarFechas(comandoFechas, inicio, finExclusiva);
                        using var lectorFechas = comandoFechas.ExecuteReader();
                        while (lectorFechas.Read())
                        {
                            modelo.FechasMayorActividad.Add(new ReporteFechaActividadViewModel
                            {
                                Fecha = Convert.ToDateTime(lectorFechas["fecha"]),
                                Llegadas = Convert.ToInt32(lectorFechas["llegadas"]),
                                Personas = Convert.ToInt32(lectorFechas["personas"])
                            });
                        }
                    }

                    using (var comando = new MySqlCommand(@"
                        SELECT COALESCE(SUM(d.subtotal),0)
                        FROM movimiento m
                        INNER JOIN tipo_movimiento tm ON tm.id_tipomov=m.id_tipomov
                        INNER JOIN detalle d ON d.id_movimiento=m.id_movimiento
                        WHERE LOWER(tm.nombre_tipomov)='venta'
                          AND m.estado='activo'
                          AND m.fecha_hora>=@inicio
                          AND m.fecha_hora<@fin_exclusiva;", conexion))
                    {
                        AgregarFechas(comando, inicio, finExclusiva);
                        modelo.IngresosPuntoVenta = Convert.ToDecimal(comando.ExecuteScalar());
                    }

                    using var comandoProductos = new MySqlCommand(@"
                        SELECT
                            COALESCE(p.nombre_proser,d.descripcion,'Producto o servicio') AS nombre,
                            COALESCE(tp.nombre,'Sin clasificación') AS tipo,
                            COALESCE(SUM(d.cantidad),0) AS cantidad,
                            COALESCE(SUM(d.subtotal),0) AS total
                        FROM movimiento m
                        INNER JOIN tipo_movimiento tm ON tm.id_tipomov=m.id_tipomov
                        INNER JOIN detalle d ON d.id_movimiento=m.id_movimiento
                        LEFT JOIN proser p ON p.id_proser=d.id_proser
                        LEFT JOIN tipo_proser tp ON tp.id_tipoproser=p.id_tipoproser
                        WHERE LOWER(tm.nombre_tipomov)='venta'
                          AND m.estado='activo'
                          AND d.id_proser IS NOT NULL
                          AND m.fecha_hora>=@inicio
                          AND m.fecha_hora<@fin_exclusiva
                        GROUP BY d.id_proser,p.nombre_proser,d.descripcion,tp.nombre
                        ORDER BY cantidad DESC,total DESC,nombre
                        LIMIT 10;", conexion);
                    AgregarFechas(comandoProductos, inicio, finExclusiva);
                    using var lectorProductos = comandoProductos.ExecuteReader();
                    while (lectorProductos.Read())
                    {
                        modelo.ProductosMasVendidos.Add(new ReporteProductoVendidoViewModel
                        {
                            Nombre = lectorProductos["nombre"]?.ToString() ?? "",
                            Tipo = lectorProductos["tipo"]?.ToString() ?? "",
                            CantidadVendida = Convert.ToInt32(lectorProductos["cantidad"]),
                            TotalVendido = Convert.ToDecimal(lectorProductos["total"])
                        });
                    }
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No fue posible generar el reporte: " + ex.Message;
                return View(modelo);
            }
        }

        private static void AgregarFechas(MySqlCommand comando, DateTime inicio, DateTime finExclusiva)
        {
            comando.Parameters.AddWithValue("@inicio", inicio);
            comando.Parameters.AddWithValue("@fin_exclusiva", finExclusiva);
        }

        private static int ObtenerEntero(MySqlConnection conexion, string consulta)
        {
            using var comando = new MySqlCommand(consulta, conexion);
            return Convert.ToInt32(comando.ExecuteScalar());
        }
    }
}
