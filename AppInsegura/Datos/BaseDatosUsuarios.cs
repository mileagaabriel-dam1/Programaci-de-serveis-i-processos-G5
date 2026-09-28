using System;
using System.Collections.Generic;
using System.Linq;
using AppInsegura.Modelos;

namespace AppInsegura.Datos
{
    // Simula una tabla de base de datos (equivalente a una tabla SQLite/sqflite).
    // No usa un motor real para que el proyecto compile sin dependencias externas.
    public class BaseDatosUsuarios
    {
        private readonly List<Usuario> usuarios = new List<Usuario>();

        // Devuelve false si ya existe un usuario con ese nombre.
        public bool Agregar(Usuario usuario)
        {
            if (BuscarPorNombre(usuario.Nombre) != null)
            {
                return false;
            }

            usuarios.Add(usuario);
            return true;
        }

        // Devuelve una copia de solo lectura: quien la reciba no puede
        // modificar la tabla interna.
        public IReadOnlyList<Usuario> ListarTodos()
        {
            return usuarios.ToList().AsReadOnly();
        }

        public bool HayAdministrador()
        {
            return usuarios.Any(u => u.Rol == Roles.Admin);
        }

        // Equivale a una consulta parametrizada con un motor real:
        //   comando.CommandText = "SELECT * FROM usuarios WHERE nombre = @nombre";
        //   comando.Parameters.AddWithValue("@nombre", nombreBuscado);
        // El valor viaja separado del texto SQL, así que nunca se interpreta como código.
        public Usuario? BuscarPorNombre(string nombreBuscado)
        {
            return usuarios.FirstOrDefault(u =>
                string.Equals(u.Nombre, nombreBuscado, StringComparison.OrdinalIgnoreCase));
        }
    }
}
