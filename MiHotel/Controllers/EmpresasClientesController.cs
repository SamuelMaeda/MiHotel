using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class EmpresasClientesController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public EmpresasClientesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarAcceso()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("IdUsuario")))
                return RedirectToAction("Login", "Acceso");

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
            return rol is "admin" or "recepcionista" ? null : RedirectToAction("Index", "Panel");
        }

        public IActionResult Index(string vista = "activos")
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            DataTable tabla = new();
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                string estado = vista == "inactivos" ? "inactivo" : "activo";
                const string sql = @"
                    SELECT e.id_empresa_cliente,e.nombre,e.estado,COUNT(cd.id_clipro) AS total_clientes
                    FROM empresa_cliente e LEFT JOIN cliente_detalle cd ON cd.id_empresa_cliente=e.id_empresa_cliente
                    WHERE e.estado=@estado GROUP BY e.id_empresa_cliente,e.nombre,e.estado ORDER BY e.nombre;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@estado", estado);
                new MySqlDataAdapter(comando).Fill(tabla);
                ViewBag.Vista = estado == "activo" ? "activos" : "inactivos";
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar las empresas: " + ex.Message;
            }
            return View(tabla);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAcceso();
            return acceso ?? View(new EmpresaClienteViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(EmpresaClienteViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (!ModelState.IsValid) return View(modelo);
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var verificar = new MySqlCommand("SELECT COUNT(*) FROM empresa_cliente WHERE LOWER(nombre)=LOWER(@nombre);", conexion);
                verificar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                if (Convert.ToInt32(verificar.ExecuteScalar()) > 0)
                {
                    ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe una empresa con este nombre.");
                    return View(modelo);
                }
                using var comando = new MySqlCommand("INSERT INTO empresa_cliente(nombre,estado) VALUES(@nombre,'activo');", conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.ExecuteNonQuery();
                TempData["Exito"] = "Empresa de procedencia creada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar la empresa: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpGet]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand("SELECT id_empresa_cliente,nombre,estado FROM empresa_cliente WHERE id_empresa_cliente=@id LIMIT 1;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            using var lector = comando.ExecuteReader();
            if (!lector.Read()) return RedirectToAction("Index");
            return View(new EmpresaClienteViewModel
            {
                IdEmpresaCliente = id,
                Nombre = lector["nombre"]?.ToString() ?? "",
                Estado = lector["estado"]?.ToString() ?? "activo"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(EmpresaClienteViewModel modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (!ModelState.IsValid) return View(modelo);
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var verificar = new MySqlCommand("SELECT COUNT(*) FROM empresa_cliente WHERE LOWER(nombre)=LOWER(@nombre) AND id_empresa_cliente<>@id;", conexion);
                verificar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                verificar.Parameters.AddWithValue("@id", modelo.IdEmpresaCliente);
                if (Convert.ToInt32(verificar.ExecuteScalar()) > 0)
                {
                    ModelState.AddModelError(nameof(modelo.Nombre), "Ya existe otra empresa con este nombre.");
                    return View(modelo);
                }
                using var comando = new MySqlCommand("UPDATE empresa_cliente SET nombre=@nombre WHERE id_empresa_cliente=@id;", conexion);
                comando.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comando.Parameters.AddWithValue("@id", modelo.IdEmpresaCliente);
                comando.ExecuteNonQuery();
                TempData["Exito"] = "Empresa actualizada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar la empresa: " + ex.Message;
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand("UPDATE empresa_cliente SET estado=IF(estado='activo','inactivo','activo') WHERE id_empresa_cliente=@id;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            comando.ExecuteNonQuery();
            TempData["Exito"] = "Estado de la empresa actualizado correctamente.";
            return RedirectToAction("Index");
        }
    }
}
