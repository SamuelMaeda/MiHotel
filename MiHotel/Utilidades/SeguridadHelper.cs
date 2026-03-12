using System.Security.Cryptography;
using System.Text;

namespace MiHotel.Utilidades
{
    public static class SeguridadHelper
    {
        public static string ObtenerSha256(string texto)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytesTexto = Encoding.UTF8.GetBytes(texto);
                byte[] bytesHash = sha256.ComputeHash(bytesTexto);

                StringBuilder builder = new StringBuilder();

                foreach (byte b in bytesHash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}