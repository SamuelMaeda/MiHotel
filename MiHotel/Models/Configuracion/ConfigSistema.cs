// ===============================
// CONFIGURACION GENERAL DEL SISTEMA
// ===============================

namespace MiHotel.Models.Configuracion
{
    public class ConfigSistema
    {
        public EmpresaConfig Empresa { get; set; } = new EmpresaConfig();

        public CertificadorConfig Certificador { get; set; } = new CertificadorConfig();

        public CredencialesConfig Credenciales { get; set; } = new CredencialesConfig();
    }
}