using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class ClientesController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public ClientesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // VALIDAR SESION ACTIVA
        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrEmpty(idUsuario);
        }

        // VALIDAR SESION
        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            return null;
        }

        // NORMALIZAR TELEFONO
        private string NormalizarTelefono(string telefono)
        {
            return telefono.Replace(" ", "").Trim();
        }

        // FORMATEAR TELEFONO
        private string FormatearTelefono(string telefono)
        {
            string telefonoLimpio = NormalizarTelefono(telefono);

            if (telefonoLimpio.Length == 8)
            {
                return telefonoLimpio.Substring(0, 4) + " " + telefonoLimpio.Substring(4, 4);
            }

            return telefono;
        }

        // OBTENER COLUMNA DE ORDEN SEGURA
        private string ObtenerColumnaOrden(string columna)
        {
            return columna.ToLower() switch
            {
                "nit" => "c.nit",
                "telefono" => "c.telefono",
                "correo" => "c.correo",
                "empresa" => "c.nombre_empresa",
                _ => "c.nombre"
            };
        }

        // OBTENER ID DE TIPO CLIENTE
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

        // LISTADO DE CLIENTES
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaClientes = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);
                string columnaOrden = ObtenerColumnaOrden(ordenarPor);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";
                string vistaNormalizada = vista.Trim().ToLower() == "inactivos" ? "inactivos" : "activos";

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string condicionEstado = vistaNormalizada == "inactivos"
                    ? "c.estado = 'inactivo'"
                    : "c.estado = 'activo'";

                string condicionBusqueda = "";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    condicionBusqueda = @"
                        AND (
                            c.nombre LIKE @busqueda
                            OR c.nit LIKE @busqueda
                            OR c.telefono LIKE @busqueda
                            OR c.correo LIKE @busqueda
                            OR c.direccion LIKE @busqueda
                            OR c.nombre_empresa LIKE @busqueda
                            OR c.numero_empresa LIKE @busqueda
                        ) ";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM clipro c
                    WHERE c.id_tipoclipro = @id_tipoclipro
                      AND {condicionEstado}
                    {condicionBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);
                comandoConteo.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

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
                        c.id_clipro,
                        c.nombre,
                        c.nit,
                        c.telefono,
                        c.correo,
                        c.direccion,
                        c.nombre_empresa,
                        c.numero_empresa,
                        c.estado
                    FROM clipro c
                    WHERE c.id_tipoclipro = @id_tipoclipro
                      AND {condicionEstado}
                    {condicionBusqueda}
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tablaClientes);

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
                ViewBag.Mensaje = "Ocurrió un error al cargar los clientes: " + ex.Message;
            }

            return View(tablaClientes);
        }

        // CREAR CLIENTE
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(string? returnUrl = null)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new ClienteAdmin());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(ClienteAdmin modelo, string? returnUrl = null)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                modelo.Telefono = NormalizarTelefono(modelo.Telefono);
                modelo.Correo = string.IsNullOrWhiteSpace(modelo.Correo) ? null : modelo.Correo.Trim();
                modelo.Nit = string.IsNullOrWhiteSpace(modelo.Nit) ? null : modelo.Nit.Trim();
                modelo.Direccion = string.IsNullOrWhiteSpace(modelo.Direccion) ? null : modelo.Direccion.Trim();
                modelo.NombreEmpresa = string.IsNullOrWhiteSpace(modelo.NombreEmpresa) ? null : modelo.NombreEmpresa.Trim();
                modelo.NumeroEmpresa = string.IsNullOrWhiteSpace(modelo.NumeroEmpresa) ? null : NormalizarTelefono(modelo.NumeroEmpresa);

                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                if (!string.IsNullOrWhiteSpace(modelo.Correo))
                {
                    string verificarCorreo = @"
                        SELECT COUNT(*)
                        FROM clipro
                        WHERE id_tipoclipro = @id_tipoclipro
                          AND correo = @correo;";

                    using var comandoVerificar = new MySqlCommand(verificarCorreo, conexion);
                    comandoVerificar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                    comandoVerificar.Parameters.AddWithValue("@correo", modelo.Correo);

                    int existe = Convert.ToInt32(comandoVerificar.ExecuteScalar());

                    if (existe > 0)
                    {
                        ViewBag.Mensaje = "El correo ya está registrado en otro cliente.";
                        modelo.Telefono = FormatearTelefono(modelo.Telefono);
                        modelo.NumeroEmpresa = string.IsNullOrWhiteSpace(modelo.NumeroEmpresa) ? null : FormatearTelefono(modelo.NumeroEmpresa);
                        return View(modelo);
                    }
                }

                string insertar = @"
                    INSERT INTO clipro
                    (
                        id_tipoclipro,
                        nombre,
                        nit,
                        telefono,
                        correo,
                        direccion,
                        nombre_empresa,
                        numero_empresa,
                        clave,
                        token_recordarme,
                        fecha_expiracion_recordarme,
                        token_recuperacion,
                        fecha_expiracion_recuperacion,
                        estado
                    )
                    VALUES
                    (
                        @id_tipoclipro,
                        @nombre,
                        @nit,
                        @telefono,
                        @correo,
                        @direccion,
                        @nombre_empresa,
                        @numero_empresa,
                        NULL,
                        NULL,
                        NULL,
                        NULL,
                        NULL,
                        'activo'
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                comandoInsertar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comandoInsertar.Parameters.AddWithValue("@nit", (object?)modelo.Nit ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@telefono", modelo.Telefono);
                comandoInsertar.Parameters.AddWithValue("@correo", (object?)modelo.Correo ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@direccion", (object?)modelo.Direccion ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@nombre_empresa", (object?)modelo.NombreEmpresa ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@numero_empresa", (object?)modelo.NumeroEmpresa ?? DBNull.Value);

                comandoInsertar.ExecuteNonQuery();

                int idClienteGenerado = Convert.ToInt32(comandoInsertar.LastInsertedId);

                TempData["Exito"] = "Cliente creado correctamente.";

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    string separador = returnUrl.Contains("?") ? "&" : "?";
                    string destino = returnUrl + separador + "idClipro=" + idClienteGenerado;

                    if (Url.IsLocalUrl(destino))
                    {
                        return Redirect(destino);
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar el cliente: " + ex.Message;
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                modelo.NumeroEmpresa = string.IsNullOrWhiteSpace(modelo.NumeroEmpresa) ? null : FormatearTelefono(modelo.NumeroEmpresa);
                return View(modelo);
            }
        }

        // EDITAR CLIENTE
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

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string consulta = @"
                    SELECT
                        id_clipro,
                        nombre,
                        nit,
                        telefono,
                        correo,
                        direccion,
                        nombre_empresa,
                        numero_empresa
                    FROM clipro
                    WHERE id_clipro = @id
                      AND id_tipoclipro = @id_tipoclipro
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el cliente solicitado.";
                    return RedirectToAction("Index");
                }

                EditarCliente modelo = new EditarCliente
                {
                    IdClipro = Convert.ToInt32(lector["id_clipro"]),
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    Nit = lector["nit"] == DBNull.Value ? null : lector["nit"].ToString(),
                    Telefono = FormatearTelefono(lector["telefono"]?.ToString() ?? ""),
                    Correo = lector["correo"] == DBNull.Value ? null : lector["correo"].ToString(),
                    Direccion = lector["direccion"] == DBNull.Value ? null : lector["direccion"].ToString(),
                    NombreEmpresa = lector["nombre_empresa"] == DBNull.Value ? null : lector["nombre_empresa"].ToString(),
                    NumeroEmpresa = lector["numero_empresa"] == DBNull.Value ? null : FormatearTelefono(lector["numero_empresa"]?.ToString() ?? "")
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el cliente: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(EditarCliente modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                modelo.Telefono = NormalizarTelefono(modelo.Telefono);
                modelo.Correo = string.IsNullOrWhiteSpace(modelo.Correo) ? null : modelo.Correo.Trim();
                modelo.Nit = string.IsNullOrWhiteSpace(modelo.Nit) ? null : modelo.Nit.Trim();
                modelo.Direccion = string.IsNullOrWhiteSpace(modelo.Direccion) ? null : modelo.Direccion.Trim();
                modelo.NombreEmpresa = string.IsNullOrWhiteSpace(modelo.NombreEmpresa) ? null : modelo.NombreEmpresa.Trim();
                modelo.NumeroEmpresa = string.IsNullOrWhiteSpace(modelo.NumeroEmpresa) ? null : NormalizarTelefono(modelo.NumeroEmpresa);

                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                if (!string.IsNullOrWhiteSpace(modelo.Correo))
                {
                    string verificarCorreo = @"
                        SELECT COUNT(*)
                        FROM clipro
                        WHERE id_tipoclipro = @id_tipoclipro
                          AND correo = @correo
                          AND id_clipro <> @id_clipro;";

                    using var comandoVerificar = new MySqlCommand(verificarCorreo, conexion);
                    comandoVerificar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);
                    comandoVerificar.Parameters.AddWithValue("@correo", modelo.Correo);
                    comandoVerificar.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);

                    int existe = Convert.ToInt32(comandoVerificar.ExecuteScalar());

                    if (existe > 0)
                    {
                        ViewBag.Mensaje = "El correo ya está registrado en otro cliente.";
                        modelo.Telefono = FormatearTelefono(modelo.Telefono);
                        modelo.NumeroEmpresa = string.IsNullOrWhiteSpace(modelo.NumeroEmpresa) ? null : FormatearTelefono(modelo.NumeroEmpresa);
                        return View(modelo);
                    }
                }

                string actualizar = @"
                    UPDATE clipro
                    SET nombre = @nombre,
                        nit = @nit,
                        telefono = @telefono,
                        correo = @correo,
                        direccion = @direccion,
                        nombre_empresa = @nombre_empresa,
                        numero_empresa = @numero_empresa
                    WHERE id_clipro = @id_clipro
                      AND id_tipoclipro = @id_tipoclipro;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comandoActualizar.Parameters.AddWithValue("@nit", (object?)modelo.Nit ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@telefono", modelo.Telefono);
                comandoActualizar.Parameters.AddWithValue("@correo", (object?)modelo.Correo ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@direccion", (object?)modelo.Direccion ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@nombre_empresa", (object?)modelo.NombreEmpresa ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@numero_empresa", (object?)modelo.NumeroEmpresa ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@id_clipro", modelo.IdClipro);
                comandoActualizar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Cliente actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar el cliente: " + ex.Message;
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                modelo.NumeroEmpresa = string.IsNullOrWhiteSpace(modelo.NumeroEmpresa) ? null : FormatearTelefono(modelo.NumeroEmpresa);
                return View(modelo);
            }
        }

        // DETALLE DE CLIENTE
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
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

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string consulta = @"
                    SELECT
                        id_clipro,
                        nombre,
                        nit,
                        telefono,
                        correo,
                        direccion,
                        nombre_empresa,
                        numero_empresa,
                        estado
                    FROM clipro
                    WHERE id_clipro = @id
                      AND id_tipoclipro = @id_tipoclipro
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el cliente solicitado.";
                    return RedirectToAction("Index");
                }

                ClienteDetalleViewModel modelo = new ClienteDetalleViewModel
                {
                    IdClipro = Convert.ToInt32(lector["id_clipro"]),
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    Nit = lector["nit"] == DBNull.Value ? null : lector["nit"].ToString(),
                    Telefono = FormatearTelefono(lector["telefono"]?.ToString() ?? ""),
                    Correo = lector["correo"] == DBNull.Value ? null : lector["correo"].ToString(),
                    Direccion = lector["direccion"] == DBNull.Value ? null : lector["direccion"].ToString(),
                    NombreEmpresa = lector["nombre_empresa"] == DBNull.Value ? null : lector["nombre_empresa"].ToString(),
                    NumeroEmpresa = lector["numero_empresa"] == DBNull.Value ? null : FormatearTelefono(lector["numero_empresa"]?.ToString() ?? ""),
                    Estado = lector["estado"]?.ToString() ?? "activo"
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el detalle del cliente: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // CAMBIAR ESTADO DE CLIENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CambiarEstado(
            int id,
            string busqueda = "",
            string ordenarPor = "nombre",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
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

                int idTipoCliente = ObtenerIdTipoCliente(conexion);

                string obtenerEstado = @"
                    SELECT estado
                    FROM clipro
                    WHERE id_clipro = @id
                      AND id_tipoclipro = @id_tipoclipro
                    LIMIT 1;";

                using var comandoEstado = new MySqlCommand(obtenerEstado, conexion);
                comandoEstado.Parameters.AddWithValue("@id", id);
                comandoEstado.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                object? resultadoEstado = comandoEstado.ExecuteScalar();

                if (resultadoEstado == null)
                {
                    TempData["Mensaje"] = "No se encontró el cliente solicitado.";
                    return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
                }

                string estadoActual = resultadoEstado.ToString()?.Trim().ToLower() ?? "inactivo";
                string nuevoEstado = estadoActual == "activo" ? "inactivo" : "activo";

                string actualizarEstado = @"
                    UPDATE clipro
                    SET estado = @estado
                    WHERE id_clipro = @id
                      AND id_tipoclipro = @id_tipoclipro;";

                using var comandoActualizar = new MySqlCommand(actualizarEstado, conexion);
                comandoActualizar.Parameters.AddWithValue("@estado", nuevoEstado);
                comandoActualizar.Parameters.AddWithValue("@id", id);
                comandoActualizar.Parameters.AddWithValue("@id_tipoclipro", idTipoCliente);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Estado del cliente actualizado correctamente.";
                return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cambiar el estado: " + ex.Message;
                return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
            }
        }
    }
}