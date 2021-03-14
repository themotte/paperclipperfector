
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PaperclipPerfector
{
    public class CommandTemplate
    {
        private readonly string sql;
        private readonly SQLiteConnection connection;

        public CommandTemplate(string sql, SQLiteConnection connection)
        {
            this.sql = sql;
            this.connection = connection;
        }

        public int ExecuteNonQuery(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddRange(parameters.Select(kvp => new SQLiteParameter(kvp.Key, kvp.Value)).ToArray());
            return command.ExecuteNonQuery();
        }

        public SQLiteDataReader ExecuteReader(IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            var command = new SQLiteCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters.Select(kvp => new SQLiteParameter(kvp.Key, kvp.Value)).ToArray());
            }
            return command.ExecuteReader();
        }

        public object ExecuteScalar(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            var command = new SQLiteCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters.Select(kvp => new SQLiteParameter(kvp.Key, kvp.Value)).ToArray());
            }
            return command.ExecuteScalar();
        }
    }
}
