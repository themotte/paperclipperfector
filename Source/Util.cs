
using System;
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

        public static SQLiteDataReader ExecuteReader(this SQLiteCommand command, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            command.Parameters.Clear();
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters.Select(kvp => new SQLiteParameter(kvp.Key, kvp.Value)).ToArray());
            }
            return command.ExecuteReader();
        }

        public static T GetField<T>(this SQLiteDataReader reader, string label)
        {
            // Inefficient, but right now I don't care.
            return reader.GetFieldValue<T>(reader.GetOrdinal(label));
        }

        public static void ContinueInBackground(this System.Threading.Tasks.Task task)
        {
            task.ContinueWith(task => Dbg.Ex(task.Exception), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        public static V TryGetValue<K, V>(this Dictionary<K, V> dict, K key)
        {
            if (dict.TryGetValue(key, out V result))
            {
                return result;
            }
            else
            {
                return default;
            }
        }

        public static T TryGetTarget<T>(this WeakReference<T> reference) where T : class
        {
            if (reference.TryGetTarget(out T result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        public static T EnumParse<T>(string input)
        {
            return (T)Enum.Parse(typeof(T), input);
        }
    }
}
