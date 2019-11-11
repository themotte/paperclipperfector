
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace PaperclipPerfector
{
    public static class Util
    {
        public static int ExecuteNonQuery(this SQLiteConnection connection, string command)
        {
            return new SQLiteCommand(command, connection).ExecuteNonQuery();
        }

        public static int ExecuteNonQuery(this SQLiteCommand command, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            command.Parameters.Clear();
            command.Parameters.AddRange(parameters.Select(kvp => new SQLiteParameter(kvp.Key, kvp.Value)).ToArray());
            return command.ExecuteNonQuery();
        }
    }
}
