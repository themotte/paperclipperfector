
using System;
using System.Collections.Concurrent;
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
        public static T GetField<T>(this System.Data.Common.DbDataReader reader, string label)
        {
            // Inefficient, but right now I don't care.
            return reader.GetFieldValue<T>(reader.GetOrdinal(label));
        }

        public static void ContinueInBackground(this System.Threading.Tasks.Task task)
        {
            task.ContinueWith(task => Dbg.Ex(task.Exception), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        public static V TryGetValue<K, V>(this IDictionary<K, V> dict, K key)
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

        public static bool TryUpdateOrAdd<K, V>(this ConcurrentDictionary<K, V> self, K key, V value, V valueOld) where V : class
        {
            if (valueOld == default(V))
            {
                return self.TryAdd(key, value);
            }
            else
            {
                return self.TryUpdate(key, value, valueOld);
            }
        }

        public static bool Remove<K, V>(this ConcurrentDictionary<K, V> self, K key, V value)
        {
            return ((IDictionary<K, V>)self).Remove(new KeyValuePair<K, V>(key, value));
        }
    }
}
