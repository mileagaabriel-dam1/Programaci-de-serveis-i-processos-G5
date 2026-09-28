using System;

namespace AppInsegura.Modelos
{
    public class Sesion
    {
        public Sesion(Usuario usuario, string token, DateTime expiraUtc)
        {
            Usuario = usuario;
            Token = token;
            ExpiraUtc = expiraUtc;
        }

        public Usuario Usuario { get; }
        public DateTime ExpiraUtc { get; }
        public bool Caducada => DateTime.UtcNow >= ExpiraUtc;

        // El token solo vive en memoria: no se muestra, no se registra en logs
        // y no se guarda en disco.
        internal string Token { get; }
    }
}
