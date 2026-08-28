using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MiHotel.Services;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class FacturasController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private readonly FacturacionService _facturacionService;

        public FacturasController(ConexionBD conexionBD, FacturacionService facturacionService)
        {
            _conexionBD = conexionBD;
            _facturacionService = facturacionService;
        }

        private bool TieneSesion() => !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("IdUsuario"));
        private bool EsAdministrador() =>
            HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() == "admin";

        private int IdUsuario()
        {
            if (!int.TryParse(HttpContext.Session.GetString("IdUsuario"), out int idUsuario))
                throw new InvalidOperationException("No fue posible identificar al usuario.");
            return idUsuario;
        }

        private IActionResult? ValidarAdministrador()
        {
            if (!TieneSesion()) return RedirectToAction("Login", "Acceso");
            if (EsAdministrador()) return null;
            TempData["Mensaje"] = "La gestión de facturas corresponde únicamente a administración.";
            return RedirectToAction("Index", "Panel");
        }

        private IActionResult? ValidarPersonal()
        {
            if (!TieneSesion()) return RedirectToAction("Login", "Acceso");
            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
            if (rol is "admin" or "recepcionista") return null;
            return Forbid();
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(string vista = "pendientes", string busqueda = "")
        {
            IActionResult? acceso = ValidarAdministrador();
            if (acceso != null) return acceso;

            vista = vista?.Trim().ToLower() ?? "pendientes";
            if (vista is not ("pendientes" or "registradas" or "no_solicitadas" or "todas"))
                vista = "pendientes";
            busqueda = busqueda?.Trim() ?? "";

            var modelo = new FacturasIndexViewModel { Vista = vista, Busqueda = busqueda };

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string filtroEstado = vista switch
                {
                    "registradas" => "rf.estado_facturacion = 'registrada'",
                    "no_solicitadas" => "rf.estado_facturacion = 'no_solicitada'",
                    "todas" => "1 = 1",
                    _ => "rf.estado_facturacion IN ('pendiente','anulada')"
                };

                string sql = $@"
                    SELECT r.id_reserva, r.id_reserva_grupo, c.nombre AS cliente,
                           c.nit AS nit_cliente, p.codigo AS habitacion,
                           r.fecha_entrada, r.fecha_salida, r.total_reserva,
                           r.estado AS estado_estadia, rf.estado_facturacion,
                           rf.fecha_decision, COALESCE(u.nombre_usuario, '-') AS usuario_decision,
                           (SELECT COUNT(*)
                            FROM documento_fiscal_reserva dfr
                            INNER JOIN documento_fiscal df
                                ON df.id_documento_fiscal = dfr.id_documento_fiscal
                            WHERE dfr.id_reserva = r.id_reserva) AS cantidad_documentos
                    FROM reserva_facturacion rf
                    INNER JOIN reserva r ON r.id_reserva = rf.id_reserva
                    INNER JOIN clipro c ON c.id_clipro = r.id_clipro
                    INNER JOIN proser p ON p.id_proser = r.id_habitacion
                    LEFT JOIN usuario u ON u.id_usuario = rf.id_usuario_decision
                    WHERE {filtroEstado}
                      AND (@busqueda = '' OR c.nombre LIKE @patron OR c.nit LIKE @patron
                           OR CAST(r.id_reserva AS CHAR) LIKE @patron
                           OR CAST(r.id_reserva_grupo AS CHAR) LIKE @patron)
                    ORDER BY COALESCE(rf.fecha_decision, r.fecha_reserva) DESC, r.id_reserva DESC;";

                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@busqueda", busqueda);
                comando.Parameters.AddWithValue("@patron", $"%{busqueda}%");
                using var lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    modelo.Registros.Add(new FacturaPendienteItemViewModel
                    {
                        IdReserva = Convert.ToInt32(lector["id_reserva"]),
                        IdReservaGrupo = lector["id_reserva_grupo"] == DBNull.Value ? null : Convert.ToInt32(lector["id_reserva_grupo"]),
                        Cliente = lector["cliente"]?.ToString() ?? "",
                        NitCliente = lector["nit_cliente"] == DBNull.Value ? null : lector["nit_cliente"].ToString(),
                        Habitacion = lector["habitacion"]?.ToString() ?? "",
                        FechaEntrada = Convert.ToDateTime(lector["fecha_entrada"]),
                        FechaSalida = Convert.ToDateTime(lector["fecha_salida"]),
                        TotalReserva = Convert.ToDecimal(lector["total_reserva"]),
                        EstadoEstadia = lector["estado_estadia"]?.ToString() ?? "",
                        EstadoFacturacion = lector["estado_facturacion"]?.ToString() ?? "sin_definir",
                        FechaDecision = lector["fecha_decision"] == DBNull.Value ? null : Convert.ToDateTime(lector["fecha_decision"]),
                        UsuarioDecision = lector["usuario_decision"]?.ToString() ?? "-",
                        CantidadDocumentos = Convert.ToInt32(lector["cantidad_documentos"])
                    });
                }

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cargar la bandeja de facturación: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Solicitar(int idReserva, string? detalle = null)
        {
            IActionResult? acceso = ValidarAdministrador();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                _facturacionService.RegistrarDecision(
                    conexion, transaccion, idReserva, true, IdUsuario(),
                    "solicitud_posterior",
                    string.IsNullOrWhiteSpace(detalle) ? "Factura solicitada después del checkout." : detalle.Trim());
                transaccion.Commit();
                TempData["Exito"] = "La reservación fue enviada a Facturas pendientes.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible registrar la solicitud: " + ex.Message;
            }

            return RedirectToAction("Detalle", "Reservas", new { id = idReserva });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarSolicitud(int idReserva, string retorno = "facturas", string busqueda = "")
        {
            IActionResult? acceso = ValidarAdministrador();
            if (acceso != null) return acceso;

            IActionResult Redireccionar() => retorno == "reservas"
                ? RedirectToAction("Index", "Reservas", new { vista = "pendientes_factura", busqueda })
                : RedirectToAction("Index", new { vista = "pendientes", busqueda });

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                string? estadoActual;
                using (var comando = new MySqlCommand(@"
                    SELECT estado_facturacion
                    FROM reserva_facturacion
                    WHERE id_reserva = @id_reserva
                    LIMIT 1
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    estadoActual = comando.ExecuteScalar()?.ToString()?.Trim().ToLower();
                }

                if (estadoActual is not ("pendiente" or "anulada"))
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "La solicitud ya no está pendiente y no puede cancelarse desde esta bandeja.";
                    return Redireccionar();
                }

                using (var comando = new MySqlCommand(@"
                    SELECT COUNT(*)
                    FROM documento_fiscal_reserva dfr
                    INNER JOIN documento_fiscal df
                        ON df.id_documento_fiscal = dfr.id_documento_fiscal
                    WHERE dfr.id_reserva = @id_reserva
                      AND df.tipo_documento = 'factura'
                      AND df.estado = 'vigente';", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    if (Convert.ToInt32(comando.ExecuteScalar()) > 0)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "La solicitud no puede cancelarse porque ya tiene una factura vigente asociada.";
                        return Redireccionar();
                    }
                }

                _facturacionService.RegistrarDecision(
                    conexion,
                    transaccion,
                    idReserva,
                    false,
                    IdUsuario(),
                    "solicitud_cancelada",
                    "El huésped indicó que ya no necesita factura.");

                transaccion.Commit();
                TempData["Exito"] = "La solicitud de factura fue cancelada.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cancelar la solicitud de factura: " + ex.Message;
            }

            return Redireccionar();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Subir(
            int idReservaOrigen,
            string nitReceptor,
            string serie,
            string numeroDte,
            string alcance,
            int? idReservaEspecifica,
            int[]? idsReservasSeleccionadas,
            IFormFile? facturaPdf)
        {
            IActionResult? acceso = ValidarAdministrador();
            if (acceso != null) return acceso;

            nitReceptor = nitReceptor?.Trim() ?? "";
            serie = serie?.Trim() ?? "";
            numeroDte = numeroDte?.Trim() ?? "";
            const string tipoDocumento = "factura";
            alcance = alcance?.Trim().ToLower() ?? "especifica";

            if (string.IsNullOrWhiteSpace(nitReceptor) || string.IsNullOrWhiteSpace(serie) || string.IsNullOrWhiteSpace(numeroDte))
            {
                TempData["Mensaje"] = "NIT o identificación del receptor, serie y número de DTE son obligatorios.";
                return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
            }

            if (nitReceptor.Length > 40 || serie.Length > 50 || numeroDte.Length > 50)
            {
                TempData["Mensaje"] = "Los datos de identificación de la factura superan la longitud permitida.";
                return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
            }

            if (facturaPdf == null || facturaPdf.Length == 0 || facturaPdf.Length > 10L * 1024L * 1024L)
            {
                TempData["Mensaje"] = "Seleccione un PDF válido de hasta 10 MB.";
                return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
            }

            string nombre = Path.GetFileName(facturaPdf.FileName).Trim();
            if (!string.Equals(Path.GetExtension(nombre), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "El documento debe estar en formato PDF.";
                return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
            }

            using var memoria = new MemoryStream();
            facturaPdf.CopyTo(memoria);
            byte[] contenido = memoria.ToArray();
            if (contenido.Length < 5 || contenido[0] != '%' || contenido[1] != 'P' || contenido[2] != 'D' || contenido[3] != 'F' || contenido[4] != '-')
            {
                TempData["Mensaje"] = "El archivo no contiene la firma de un PDF válido.";
                return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                int? idGrupo;
                using (var comando = new MySqlCommand("SELECT id_reserva_grupo FROM reserva WHERE id_reserva=@id LIMIT 1 FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id", idReservaOrigen);
                    object? resultado = comando.ExecuteScalar();
                    if (resultado == null)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "No se encontró la reservación.";
                        return RedirectToAction("Index", "Reservas");
                    }
                    idGrupo = resultado == DBNull.Value ? null : Convert.ToInt32(resultado);
                }

                var idsGrupo = new List<int>();
                if (idGrupo.HasValue)
                {
                    using var comando = new MySqlCommand(@"
                        SELECT id_reserva FROM reserva
                        WHERE id_reserva_grupo=@grupo AND estado<>'cancelada'
                        ORDER BY fecha_entrada,id_reserva FOR UPDATE;", conexion, transaccion);
                    comando.Parameters.AddWithValue("@grupo", idGrupo.Value);
                    using var lector = comando.ExecuteReader();
                    while (lector.Read()) idsGrupo.Add(Convert.ToInt32(lector["id_reserva"]));
                }
                else
                {
                    idsGrupo.Add(idReservaOrigen);
                }

                List<int> destinos = alcance switch
                {
                    "grupo" when idGrupo.HasValue => idsGrupo,
                    "especifica" when idReservaEspecifica.HasValue => [idReservaEspecifica.Value],
                    "varias" => (idsReservasSeleccionadas ?? []).Distinct().ToList(),
                    _ => [idReservaOrigen]
                };

                if (destinos.Count == 0 || destinos.Any(id => !idsGrupo.Contains(id)) || (alcance == "varias" && destinos.Count < 2))
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "Seleccione correctamente las estadías cubiertas por el documento.";
                    return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
                }

                using (var comando = new MySqlCommand(@"
                    SELECT COUNT(*) FROM documento_fiscal
                    WHERE serie=@serie AND numero_dte=@numero AND estado<>'sustituido';", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@serie", serie);
                    comando.Parameters.AddWithValue("@numero", numeroDte);
                    if (Convert.ToInt32(comando.ExecuteScalar()) > 0)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Ya existe un documento con la misma serie y número de DTE.";
                        return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
                    }
                }

                long idDocumento;
                using (var comando = new MySqlCommand(@"
                    INSERT INTO documento_fiscal
                        (tipo_documento,nit_receptor,serie,numero_dte,contenido,tipo_mime,
                         nombre_original,tamano,estado,id_usuario_registro,fecha_registro)
                    VALUES
                        (@tipo,@nit,@serie,@numero,@contenido,'application/pdf',
                         @nombre,@tamano,'vigente',@usuario,CURRENT_TIMESTAMP);", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@tipo", tipoDocumento);
                    comando.Parameters.AddWithValue("@nit", nitReceptor);
                    comando.Parameters.AddWithValue("@serie", serie);
                    comando.Parameters.AddWithValue("@numero", numeroDte);
                    comando.Parameters.Add("@contenido", MySqlDbType.LongBlob).Value = contenido;
                    comando.Parameters.AddWithValue("@nombre", string.IsNullOrWhiteSpace(nombre) ? "factura.pdf" : nombre[..Math.Min(nombre.Length, 255)]);
                    comando.Parameters.AddWithValue("@tamano", contenido.LongLength);
                    comando.Parameters.AddWithValue("@usuario", IdUsuario());
                    comando.ExecuteNonQuery();
                    idDocumento = comando.LastInsertedId;
                }

                foreach (int idReserva in destinos)
                {
                    using var comando = new MySqlCommand(@"
                        INSERT INTO documento_fiscal_reserva (id_documento_fiscal,id_reserva)
                        VALUES (@documento,@reserva);", conexion, transaccion);
                    comando.Parameters.AddWithValue("@documento", idDocumento);
                    comando.Parameters.AddWithValue("@reserva", idReserva);
                    comando.ExecuteNonQuery();
                }

                _facturacionService.MarcarRegistrada(conexion, transaccion, destinos, IdUsuario(), idDocumento);
                transaccion.Commit();
                TempData["Exito"] = destinos.Count == 1
                    ? "Factura registrada correctamente."
                    : $"Factura registrada para {destinos.Count} estadías.";
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                TempData["Mensaje"] = "Ya existe un documento con la misma serie y número de DTE.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible registrar el documento: " + ex.Message;
            }

            return RedirectToAction("Detalle", "Reservas", new { id = idReservaOrigen });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult DocumentoPdf(long id, bool descargar = false)
        {
            IActionResult? acceso = ValidarPersonal();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                SELECT contenido,nombre_original FROM documento_fiscal
                WHERE id_documento_fiscal=@id LIMIT 1;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            using var lector = comando.ExecuteReader(CommandBehavior.SequentialAccess);
            if (!lector.Read()) return NotFound();
            Response.Headers.CacheControl = "no-store, no-cache";
            byte[] contenido = (byte[])lector["contenido"];
            string nombre = lector["nombre_original"]?.ToString() ?? "factura.pdf";
            return descargar ? File(contenido, "application/pdf", nombre) : File(contenido, "application/pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Anular(long idDocumentoFiscal, int idReservaRetorno, string motivo)
        {
            IActionResult? acceso = ValidarAdministrador();
            if (acceso != null) return acceso;
            motivo = motivo?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 255)
            {
                TempData["Mensaje"] = "Debe indicar un motivo de anulación de hasta 255 caracteres.";
                return RedirectToAction("Detalle", "Reservas", new { id = idReservaRetorno });
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                using var comando = new MySqlCommand(@"
                    UPDATE documento_fiscal
                    SET estado='anulado',id_usuario_estado=@usuario,
                        fecha_estado=CURRENT_TIMESTAMP,motivo_estado=@motivo
                    WHERE id_documento_fiscal=@id AND estado='vigente';", conexion, transaccion);
                comando.Parameters.AddWithValue("@usuario", IdUsuario());
                comando.Parameters.AddWithValue("@motivo", motivo);
                comando.Parameters.AddWithValue("@id", idDocumentoFiscal);
                if (comando.ExecuteNonQuery() == 0)
                {
                    transaccion.Rollback();
                    TempData["Mensaje"] = "El documento no existe o ya no está vigente.";
                    return RedirectToAction("Detalle", "Reservas", new { id = idReservaRetorno });
                }

                var reservas = new List<int>();
                using (var leer = new MySqlCommand("SELECT id_reserva FROM documento_fiscal_reserva WHERE id_documento_fiscal=@id;", conexion, transaccion))
                {
                    leer.Parameters.AddWithValue("@id", idDocumentoFiscal);
                    using var lector = leer.ExecuteReader();
                    while (lector.Read()) reservas.Add(Convert.ToInt32(lector["id_reserva"]));
                }

                foreach (int idReserva in reservas)
                {
                    bool conservaFacturaVigente;
                    using (var verificar = new MySqlCommand(@"
                        SELECT EXISTS(
                            SELECT 1
                            FROM documento_fiscal_reserva dfr
                            INNER JOIN documento_fiscal df
                                ON df.id_documento_fiscal = dfr.id_documento_fiscal
                            WHERE dfr.id_reserva = @reserva
                              AND df.tipo_documento = 'factura'
                              AND df.estado = 'vigente'
                        );", conexion, transaccion))
                    {
                        verificar.Parameters.AddWithValue("@reserva", idReserva);
                        conservaFacturaVigente = Convert.ToBoolean(verificar.ExecuteScalar());
                    }

                    string estadoNuevo = conservaFacturaVigente ? "registrada" : "anulada";
                    string estadoAdministrativo = conservaFacturaVigente ? "cerrado" : "pendiente_revision";
                    using var actualizar = new MySqlCommand(@"
                        UPDATE reserva_facturacion
                        SET estado_facturacion=@estado,estado_administrativo=@administrativo,
                            id_usuario_actualizacion=@usuario
                        WHERE id_reserva=@reserva;", conexion, transaccion);
                    actualizar.Parameters.AddWithValue("@estado", estadoNuevo);
                    actualizar.Parameters.AddWithValue("@administrativo", estadoAdministrativo);
                    actualizar.Parameters.AddWithValue("@usuario", IdUsuario());
                    actualizar.Parameters.AddWithValue("@reserva", idReserva);
                    actualizar.ExecuteNonQuery();
                    _facturacionService.RegistrarHistorial(
                        conexion, transaccion, idReserva, "documento_anulado", true, true,
                        "registrada", estadoNuevo,
                        conservaFacturaVigente
                            ? motivo + " La reservación conserva otra factura vigente."
                            : motivo,
                        IdUsuario());
                }

                transaccion.Commit();
                TempData["Exito"] = "Documento anulado. El historial fue conservado para auditoría.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible anular el documento: " + ex.Message;
            }

            return RedirectToAction("Detalle", "Reservas", new { id = idReservaRetorno });
        }
    }
}
