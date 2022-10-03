
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PaperclipPerfector
{
    public class CommandTemplatePostgres
    {
        private readonly string sql;
        private readonly NpgsqlConnection connection;

        public CommandTemplatePostgres(string sql, NpgsqlConnection connection)
        {
            this.sql = sql;
            this.connection = connection;
        }

        public int ExecuteNonQuery(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddRange(parameters.Select(kvp => new NpgsqlParameter(kvp.Key, kvp.Value)).ToArray());
            return command.ExecuteNonQuery();
        }

        public NpgsqlDataReader ExecuteReader(IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            var command = new NpgsqlCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters.Select(kvp => new NpgsqlParameter(kvp.Key, kvp.Value)).ToArray());
            }
            return command.ExecuteReader();
        }

        public object ExecuteScalar(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            var command = new NpgsqlCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters.Select(kvp => new NpgsqlParameter(kvp.Key, kvp.Value)).ToArray());
            }
            return command.ExecuteScalar();
        }
    }
}
