
using System;

namespace PaperclipPerfector
{
    public class DbBacked<T>
    {
        private T value;
        private readonly string key;
        private readonly Func<T, string> serialize;
        private readonly Func<string, T> deserialize;

        public DbBacked(string key, T start, Func<T, string> serialize, Func<string, T> deserialize)
        {
            this.key = key;
            this.serialize = serialize;
            this.deserialize = deserialize;

            string db = Db.Instance.ReadGlobalProp(key);

            if (db == null)
            {
                value = start;
            }
            else
            {
                value = deserialize(db);
            }
        }

        public T Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
                Db.Instance.WriteGlobalProp(key, serialize(this.value));
            }
        }
    }

    public class GlobalProps
    {
        public DbBacked<DateTimeOffset> lastScraped = new DbBacked<DateTimeOffset>("lastScraped", DateTimeOffset.Now - TimeSpan.FromDays(30), item => item.ToString(), str => DateTimeOffset.Parse(str));

        private static GlobalProps StoredInstance;
        public static GlobalProps Instance
        {
            get
            {
                if (StoredInstance == null)
                {
                    StoredInstance = new GlobalProps();
                }

                return StoredInstance;
            }
        }
    }
}
