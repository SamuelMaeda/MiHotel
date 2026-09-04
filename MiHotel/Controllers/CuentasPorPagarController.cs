using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;

namespace MiHotel.Controllers
{
    public class CuentasPorPagarController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public CuentasPorPagarController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarAcceso()
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("IdUsuario")))
                return RedirectToAction("Login", "Acceso");

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLowerInvariant() ?? "";
            return rol == "admin" ? null : RedirectToAction("Index", "Panel");
        }

        private int ObtenerIdUsuario()
        {
            return int.TryParse(HttpContext.Session.GetString("IdUsuario"), out int id) ? id : 0;
        }

        private static DateTime PrimerDiaDelMes(DateTime fecha) => new(fecha.Year, fecha.Month, 1);

        private static string NormalizarVista(string vista) => vista switch
        {
            "pagados" => "pagados",
            "desactivados" => "desactivados",
            _ => "por_pagar"
        };

        private static string ColumnaOrden(string columna) => columna.ToLowerInvariant() switch
        {
            "descripcion" => "c.descripcion",
            "tipo" => "c.tipo_monto",
            "monto" => "c.monto_mensual",
            "vencimiento" => "c.dia_vencimiento",
            "meses" => "meses_pendientes",
            "saldo" => "saldo_pendiente",
            "ultimo_pago" => "fecha_ultimo_pago",
            "registro" => "c.fecha_registro",
            _ => "c.nombre"
        };

        private void SincronizarMensualidades(MySqlConnection conexion, MySqlTransaction transaccion)
        {
            DateTime mesActual = PrimerDiaDelMes(DateTime.Today);
            var cuentas = new List<(int Id, string TipoMonto, decimal? Monto, DateTime ProximoPeriodo)>();

            using (var comando = new MySqlCommand(@"
                SELECT id_cuenta_por_pagar,tipo_monto,monto_mensual,proximo_periodo
                FROM cuenta_por_pagar
                WHERE activa=1 AND proximo_periodo<=@mes_actual
                FOR UPDATE;", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@mes_actual", mesActual);
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    cuentas.Add((
                        Convert.ToInt32(lector["id_cuenta_por_pagar"]),
                        lector["tipo_monto"]?.ToString() ?? "fijo",
                        lector["monto_mensual"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(lector["monto_mensual"]),
                        PrimerDiaDelMes(Convert.ToDateTime(lector["proximo_periodo"]))));
                }
            }

            foreach (var cuenta in cuentas)
            {
                DateTime periodo = cuenta.ProximoPeriodo;
                while (periodo <= mesActual)
                {
                    bool esVariable = cuenta.TipoMonto == "variable";
                    using var insertar = new MySqlCommand(@"
                        INSERT IGNORE INTO cuenta_por_pagar_periodo
                            (id_cuenta_por_pagar,periodo,monto_generado,estado,fecha_generacion)
                        VALUES (@id,@periodo,@monto,@estado,CURRENT_TIMESTAMP);", conexion, transaccion);
                    insertar.Parameters.AddWithValue("@id", cuenta.Id);
                    insertar.Parameters.AddWithValue("@periodo", periodo);
                    insertar.Parameters.AddWithValue("@monto", esVariable ? DBNull.Value : cuenta.Monto);
                    insertar.Parameters.AddWithValue("@estado", esVariable ? "pendiente_monto" : "pendiente");
                    insertar.ExecuteNonQuery();
                    periodo = periodo.AddMonths(1);
                }

                using var actualizar = new MySqlCommand(@"
                    UPDATE cuenta_por_pagar
                    SET proximo_periodo=@proximo
                    WHERE id_cuenta_por_pagar=@id;", conexion, transaccion);
                actualizar.Parameters.AddWithValue("@proximo", periodo);
                actualizar.Parameters.AddWithValue("@id", cuenta.Id);
                actualizar.ExecuteNonQuery();
            }
        }

        private void SincronizarMensualidades(MySqlConnection conexion)
        {
            using var transaccion = conexion.BeginTransaction();
            SincronizarMensualidades(conexion, transaccion);
            transaccion.Commit();
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string vista = "por_pagar",
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            var modelo = new CuentasPorPagarIndexViewModel
            {
                Vista = NormalizarVista(vista),
                Busqueda = busqueda?.Trim() ?? "",
                OrdenarPor = ordenarPor,
                Direccion = direccion.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc"
            };

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                SincronizarMensualidades(conexion);

                string filtroEstado = modelo.Vista switch
                {
                    "pagados" => "c.activa=1 AND COALESCE(pendientes.meses_pendientes,0)=0",
                    "desactivados" => "c.activa=0",
                    _ => "c.activa=1 AND COALESCE(pendientes.meses_pendientes,0)>0"
                };
                string filtroBusqueda = string.IsNullOrWhiteSpace(modelo.Busqueda)
                    ? ""
                    : " AND (c.nombre LIKE @busqueda OR c.descripcion LIKE @busqueda)";
                string columna = ColumnaOrden(ordenarPor);
                string sentido = modelo.Direccion == "desc" ? "DESC" : "ASC";

                string sql = $@"
                    SELECT c.id_cuenta_por_pagar,c.nombre,c.descripcion,c.tipo_monto,c.monto_mensual,
                           c.dia_vencimiento,c.activa,c.fecha_registro,
                           COALESCE(pendientes.meses_pendientes,0) AS meses_pendientes,
                           COALESCE(pendientes.periodos_sin_monto,0) AS periodos_sin_monto,
                           COALESCE(pendientes.saldo_pendiente,0) AS saldo_pendiente,
                           pagos.fecha_ultimo_pago
                    FROM cuenta_por_pagar c
                    LEFT JOIN (
                        SELECT id_cuenta_por_pagar,
                               COUNT(*) AS meses_pendientes,
                               SUM(CASE WHEN estado='pendiente_monto' THEN 1 ELSE 0 END) AS periodos_sin_monto,
                               SUM(CASE WHEN estado='pendiente' THEN monto_generado ELSE 0 END) AS saldo_pendiente
                        FROM cuenta_por_pagar_periodo
                        WHERE estado IN ('pendiente','pendiente_monto')
                        GROUP BY id_cuenta_por_pagar
                    ) pendientes ON pendientes.id_cuenta_por_pagar=c.id_cuenta_por_pagar
                    LEFT JOIN (
                        SELECT id_cuenta_por_pagar,MAX(fecha_pago) AS fecha_ultimo_pago
                        FROM pago_cuenta_por_pagar
                        GROUP BY id_cuenta_por_pagar
                    ) pagos ON pagos.id_cuenta_por_pagar=c.id_cuenta_por_pagar
                    WHERE {filtroEstado} {filtroBusqueda}
                    ORDER BY {columna} {sentido},c.id_cuenta_por_pagar ASC;";

                var registros = new List<CuentaPorPagarResumenViewModel>();
                using var comando = new MySqlCommand(sql, conexion);
                if (filtroBusqueda.Length > 0)
                    comando.Parameters.AddWithValue("@busqueda", "%" + modelo.Busqueda + "%");
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    registros.Add(new CuentaPorPagarResumenViewModel
                    {
                        IdCuentaPorPagar = Convert.ToInt32(lector["id_cuenta_por_pagar"]),
                        Nombre = lector["nombre"]?.ToString() ?? "",
                        Descripcion = lector["descripcion"]?.ToString() ?? "",
                        TipoMonto = lector["tipo_monto"]?.ToString() ?? "fijo",
                        MontoMensual = lector["monto_mensual"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(lector["monto_mensual"]),
                        DiaVencimiento = Convert.ToInt32(lector["dia_vencimiento"]),
                        Activa = Convert.ToBoolean(lector["activa"]),
                        MesesPendientes = Convert.ToInt32(lector["meses_pendientes"]),
                        PeriodosSinMonto = Convert.ToInt32(lector["periodos_sin_monto"]),
                        SaldoPendiente = Convert.ToDecimal(lector["saldo_pendiente"]),
                        FechaUltimoPago = lector["fecha_ultimo_pago"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(lector["fecha_ultimo_pago"]),
                        FechaRegistro = Convert.ToDateTime(lector["fecha_registro"])
                    });
                }

                modelo.TotalRegistros = registros.Count;
                modelo.SaldoTotal = registros.Sum(r => r.SaldoPendiente);
                modelo.PeriodosSinMontoTotal = registros.Sum(r => r.PeriodosSinMonto);
                modelo.TotalPaginas = Math.Max(1, (int)Math.Ceiling(registros.Count / (double)RegistrosPorPagina));
                modelo.PaginaActual = Math.Min(Math.Max(1, pagina), modelo.TotalPaginas);
                modelo.Registros = registros
                    .Skip((modelo.PaginaActual - 1) * RegistrosPorPagina)
                    .Take(RegistrosPorPagina)
                    .ToList();
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No fue posible cargar las cuentas por pagar: " + ex.Message;
            }

            return View(modelo);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAcceso();
            return acceso ?? View(new CuentaPorPagarFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(CuentaPorPagarFormViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            modelo.TipoMonto = modelo.TipoMonto?.Trim().ToLowerInvariant() ?? "";
            if (modelo.TipoMonto is not ("fijo" or "variable"))
                ModelState.AddModelError(nameof(modelo.TipoMonto), "Seleccione un tipo de monto válido.");
            if (modelo.TipoMonto == "fijo" && (!modelo.MontoMensual.HasValue || modelo.MontoMensual <= 0))
                ModelState.AddModelError(nameof(modelo.MontoMensual), "Ingrese el monto mensual de la cuenta fija.");
            if (modelo.TipoMonto == "variable") modelo.MontoMensual = null;
            if (!ModelState.IsValid) return View(modelo);

            try
            {
                DateTime periodo = PrimerDiaDelMes(DateTime.Today);
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                using var comando = new MySqlCommand(@"
                    INSERT INTO cuenta_por_pagar
                        (nombre,descripcion,tipo_monto,monto_mensual,dia_vencimiento,activa,
                         proximo_periodo,fecha_registro,fecha_modificacion)
                    VALUES (@nombre,@descripcion,@tipo,@monto,@dia,1,@proximo,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);", conexion, transaccion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(modelo.Descripcion) ? DBNull.Value : modelo.Descripcion.Trim());
                comando.Parameters.AddWithValue("@tipo", modelo.TipoMonto);
                comando.Parameters.AddWithValue("@monto", modelo.MontoMensual.HasValue ? modelo.MontoMensual.Value : DBNull.Value);
                comando.Parameters.AddWithValue("@dia", modelo.DiaVencimiento);
                comando.Parameters.AddWithValue("@proximo", periodo.AddMonths(1));
                comando.ExecuteNonQuery();
                int id = Convert.ToInt32(comando.LastInsertedId);

                using var mensualidad = new MySqlCommand(@"
                    INSERT INTO cuenta_por_pagar_periodo
                        (id_cuenta_por_pagar,periodo,monto_generado,estado,fecha_generacion)
                    VALUES (@id,@periodo,@monto,@estado,CURRENT_TIMESTAMP);", conexion, transaccion);
                mensualidad.Parameters.AddWithValue("@id", id);
                mensualidad.Parameters.AddWithValue("@periodo", periodo);
                mensualidad.Parameters.AddWithValue("@monto", modelo.MontoMensual.HasValue ? modelo.MontoMensual.Value : DBNull.Value);
                mensualidad.Parameters.AddWithValue("@estado", modelo.TipoMonto == "variable" ? "pendiente_monto" : "pendiente");
                mensualidad.ExecuteNonQuery();

                transaccion.Commit();
                TempData["Exito"] = "Cuenta por pagar creada y agregada al mes actual.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No fue posible crear la cuenta por pagar: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpGet]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                SincronizarMensualidades(conexion);
                using var comando = new MySqlCommand(@"
                    SELECT id_cuenta_por_pagar,nombre,descripcion,tipo_monto,monto_mensual,dia_vencimiento
                    FROM cuenta_por_pagar
                    WHERE id_cuenta_por_pagar=@id
                    LIMIT 1;", conexion);
                comando.Parameters.AddWithValue("@id", id);
                using var lector = comando.ExecuteReader();
                if (!lector.Read())
                {
                    TempData["Mensaje"] = "La cuenta por pagar no existe.";
                    return RedirectToAction("Index");
                }

                return View(new CuentaPorPagarFormViewModel
                {
                    IdCuentaPorPagar = id,
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    Descripcion = lector["descripcion"]?.ToString(),
                    TipoMonto = lector["tipo_monto"]?.ToString() ?? "fijo",
                    MontoMensual = lector["monto_mensual"] == DBNull.Value
                        ? null
                        : Convert.ToDecimal(lector["monto_mensual"]),
                    DiaVencimiento = Convert.ToInt32(lector["dia_vencimiento"])
                });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cargar la cuenta por pagar: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(CuentaPorPagarFormViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            modelo.TipoMonto = modelo.TipoMonto?.Trim().ToLowerInvariant() ?? "";
            if (modelo.TipoMonto is not ("fijo" or "variable"))
                ModelState.AddModelError(nameof(modelo.TipoMonto), "Seleccione un tipo de monto válido.");
            if (modelo.TipoMonto == "fijo" && (!modelo.MontoMensual.HasValue || modelo.MontoMensual <= 0))
                ModelState.AddModelError(nameof(modelo.MontoMensual), "Ingrese el monto mensual de la cuenta fija.");
            if (modelo.TipoMonto == "variable") modelo.MontoMensual = null;
            if (!ModelState.IsValid) return View(modelo);

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                SincronizarMensualidades(conexion);
                using var comando = new MySqlCommand(@"
                    UPDATE cuenta_por_pagar
                    SET nombre=@nombre,descripcion=@descripcion,tipo_monto=@tipo,monto_mensual=@monto,
                        dia_vencimiento=@dia,fecha_modificacion=CURRENT_TIMESTAMP
                    WHERE id_cuenta_por_pagar=@id;", conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(modelo.Descripcion) ? DBNull.Value : modelo.Descripcion.Trim());
                comando.Parameters.AddWithValue("@tipo", modelo.TipoMonto);
                comando.Parameters.AddWithValue("@monto", modelo.MontoMensual.HasValue ? modelo.MontoMensual.Value : DBNull.Value);
                comando.Parameters.AddWithValue("@dia", modelo.DiaVencimiento);
                comando.Parameters.AddWithValue("@id", modelo.IdCuentaPorPagar);
                if (comando.ExecuteNonQuery() == 0)
                    TempData["Mensaje"] = "La cuenta por pagar no existe.";
                else
                    TempData["Exito"] = "Cuenta por pagar actualizada. El nuevo monto se aplicará a los meses futuros.";
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "No fue posible actualizar la cuenta por pagar: " + ex.Message;
                return View(modelo);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Pagar(int id, string vista = "por_pagar")
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                SincronizarMensualidades(conexion, transaccion);

                decimal total = 0;
                int periodos = 0;
                int periodosSinMonto = 0;
                using (var bloquear = new MySqlCommand(@"
                    SELECT monto_generado,estado
                    FROM cuenta_por_pagar_periodo
                    WHERE id_cuenta_por_pagar=@id AND estado IN ('pendiente','pendiente_monto')
                    FOR UPDATE;", conexion, transaccion))
                {
                    bloquear.Parameters.AddWithValue("@id", id);
                    using var lector = bloquear.ExecuteReader();
                    while (lector.Read())
                    {
                        if ((lector["estado"]?.ToString() ?? "") == "pendiente_monto")
                        {
                            periodosSinMonto++;
                        }
                        else
                        {
                            total += Convert.ToDecimal(lector["monto_generado"]);
                            periodos++;
                        }
                    }
                }

                if (periodosSinMonto > 0)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Primero debe ingresar el importe de todas las mensualidades variables pendientes.";
                    return RedirectToAction("Montos", new { id });
                }

                if (periodos == 0)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Esta cuenta no tiene mensualidades pendientes.";
                    return RedirectToAction("Index", new { vista });
                }

                using (var actualizar = new MySqlCommand(@"
                    UPDATE cuenta_por_pagar_periodo
                    SET estado='pagado',fecha_pago=CURRENT_TIMESTAMP
                    WHERE id_cuenta_por_pagar=@id AND estado='pendiente';", conexion, transaccion))
                {
                    actualizar.Parameters.AddWithValue("@id", id);
                    actualizar.ExecuteNonQuery();
                }

                using (var pago = new MySqlCommand(@"
                    INSERT INTO pago_cuenta_por_pagar
                        (id_cuenta_por_pagar,monto,periodos_cubiertos,fecha_pago,id_usuario)
                    VALUES (@id,@monto,@periodos,CURRENT_TIMESTAMP,@usuario);", conexion, transaccion))
                {
                    pago.Parameters.AddWithValue("@id", id);
                    pago.Parameters.AddWithValue("@monto", total);
                    pago.Parameters.AddWithValue("@periodos", periodos);
                    pago.Parameters.AddWithValue("@usuario", ObtenerIdUsuario());
                    pago.ExecuteNonQuery();
                }

                transaccion.Commit();
                TempData["Exito"] = $"Pago registrado por Q {total:N2}.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible registrar el pago: " + ex.Message;
            }

            string vistaDestino = NormalizarVista(vista) == "desactivados" ? "desactivados" : "pagados";
            return RedirectToAction("Index", new { vista = vistaDestino });
        }

        [HttpGet]
        public IActionResult Montos(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                SincronizarMensualidades(conexion);

                var modelo = new CuentaPorPagarMontosViewModel { IdCuentaPorPagar = id };
                using (var cuenta = new MySqlCommand(@"
                    SELECT nombre
                    FROM cuenta_por_pagar
                    WHERE id_cuenta_por_pagar=@id
                    LIMIT 1;", conexion))
                {
                    cuenta.Parameters.AddWithValue("@id", id);
                    object? nombre = cuenta.ExecuteScalar();
                    if (nombre == null)
                    {
                        TempData["Mensaje"] = "La cuenta variable no existe.";
                        return RedirectToAction("Index");
                    }
                    modelo.Nombre = nombre.ToString() ?? "";
                }

                using (var periodos = new MySqlCommand(@"
                    SELECT id_periodo,periodo,monto_generado,estado
                    FROM cuenta_por_pagar_periodo
                    WHERE id_cuenta_por_pagar=@id AND estado='pendiente_monto'
                    ORDER BY periodo ASC;", conexion))
                {
                    periodos.Parameters.AddWithValue("@id", id);
                    using var lector = periodos.ExecuteReader();
                    while (lector.Read())
                    {
                        modelo.Periodos.Add(new CuentaPorPagarPeriodoViewModel
                        {
                            IdPeriodo = Convert.ToInt64(lector["id_periodo"]),
                            Periodo = Convert.ToDateTime(lector["periodo"]),
                            MontoGenerado = lector["monto_generado"] == DBNull.Value
                                ? null
                                : Convert.ToDecimal(lector["monto_generado"]),
                            Estado = lector["estado"]?.ToString() ?? "pendiente_monto"
                        });
                    }
                }

                if (modelo.Periodos.Count == 0)
                {
                    TempData["Mensaje"] = "Esta cuenta no tiene mensualidades pendientes de monto.";
                    return RedirectToAction("Index");
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cargar las mensualidades variables: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DefinirMonto(int idCuentaPorPagar, long idPeriodo, decimal monto)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (monto <= 0)
            {
                TempData["Mensaje"] = "El monto de la mensualidad debe ser mayor que cero.";
                return RedirectToAction("Montos", new { id = idCuentaPorPagar });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                using var comando = new MySqlCommand(@"
                    UPDATE cuenta_por_pagar_periodo periodo
                    INNER JOIN cuenta_por_pagar cuenta
                        ON cuenta.id_cuenta_por_pagar=periodo.id_cuenta_por_pagar
                    SET periodo.monto_generado=@monto,
                        periodo.estado='pendiente'
                    WHERE periodo.id_periodo=@periodo
                      AND periodo.id_cuenta_por_pagar=@cuenta
                      AND periodo.estado='pendiente_monto';", conexion, transaccion);
                comando.Parameters.AddWithValue("@monto", monto);
                comando.Parameters.AddWithValue("@periodo", idPeriodo);
                comando.Parameters.AddWithValue("@cuenta", idCuentaPorPagar);
                int afectados = comando.ExecuteNonQuery();

                using var pendientes = new MySqlCommand(@"
                    SELECT COUNT(*)
                    FROM cuenta_por_pagar_periodo
                    WHERE id_cuenta_por_pagar=@id AND estado='pendiente_monto';", conexion, transaccion);
                pendientes.Parameters.AddWithValue("@id", idCuentaPorPagar);
                int restantes = Convert.ToInt32(pendientes.ExecuteScalar());

                if (afectados == 0)
                    TempData["Mensaje"] = "La mensualidad ya tenía un monto o no pertenece a esta cuenta.";
                else
                    TempData["Exito"] = "Monto mensual registrado correctamente.";

                transaccion.Commit();
                return restantes > 0
                    ? RedirectToAction("Montos", new { id = idCuentaPorPagar })
                    : RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible registrar el monto: " + ex.Message;
                return RedirectToAction("Montos", new { id = idCuentaPorPagar });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Desactivar(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                SincronizarMensualidades(conexion, transaccion);
                using var comando = new MySqlCommand(@"
                    UPDATE cuenta_por_pagar
                    SET activa=0,fecha_desactivacion=CURRENT_TIMESTAMP,fecha_modificacion=CURRENT_TIMESTAMP
                    WHERE id_cuenta_por_pagar=@id AND activa=1;", conexion, transaccion);
                comando.Parameters.AddWithValue("@id", id);
                int afectados = comando.ExecuteNonQuery();
                transaccion.Commit();
                TempData[afectados == 0 ? "Mensaje" : "Exito"] = afectados == 0
                    ? "La cuenta no existe o ya estaba desactivada."
                    : "Cuenta desactivada. Ya no generará cargos en los meses siguientes.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible desactivar la cuenta: " + ex.Message;
            }
            return RedirectToAction("Index", new { vista = "desactivados" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Activar(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                DateTime mesActual = PrimerDiaDelMes(DateTime.Today);
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                bool existePeriodoActual;
                using (var existe = new MySqlCommand(@"
                    SELECT EXISTS(
                        SELECT 1 FROM cuenta_por_pagar_periodo
                        WHERE id_cuenta_por_pagar=@id AND periodo=@periodo
                    );", conexion, transaccion))
                {
                    existe.Parameters.AddWithValue("@id", id);
                    existe.Parameters.AddWithValue("@periodo", mesActual);
                    existePeriodoActual = Convert.ToInt32(existe.ExecuteScalar()) == 1;
                }

                using var comando = new MySqlCommand(@"
                    UPDATE cuenta_por_pagar
                    SET activa=1,fecha_desactivacion=NULL,fecha_modificacion=CURRENT_TIMESTAMP,
                        proximo_periodo=@proximo
                    WHERE id_cuenta_por_pagar=@id AND activa=0;", conexion, transaccion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@proximo", existePeriodoActual ? mesActual.AddMonths(1) : mesActual);
                int afectados = comando.ExecuteNonQuery();
                if (afectados > 0) SincronizarMensualidades(conexion, transaccion);
                transaccion.Commit();

                TempData[afectados == 0 ? "Mensaje" : "Exito"] = afectados == 0
                    ? "La cuenta no existe o ya estaba activa."
                    : "Cuenta activada nuevamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible activar la cuenta: " + ex.Message;
            }
            return RedirectToAction("Index");
        }
    }
}
