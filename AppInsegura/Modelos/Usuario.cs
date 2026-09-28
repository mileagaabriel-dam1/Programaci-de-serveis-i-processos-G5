namespace AppInsegura.Modelos
{
    public static class Roles
    {
        public const string Admin = "admin";
        public const string Jugador = "jugador";
    }

    public class Usuario
    {
        public Usuario(string nombre, string rol, byte[] sal, byte[] contrasenaHash)
        {
            Nombre = nombre;
            Rol = rol;
            Sal = sal;
            ContrasenaHash = contrasenaHash;
        }

        // Sin setters públicos: el rol o el hash no se pueden cambiar desde fuera.
        public string Nombre { get; }
        public string Rol { get; }

        // Solo el servicio de autenticación necesita estos datos.
        internal byte[] Sal { get; }
        internal byte[] ContrasenaHash { get; }
    }
}
