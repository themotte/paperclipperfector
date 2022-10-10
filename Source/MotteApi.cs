using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PaperclipPerfector
{
    public class MotteApi
    {
        public class Post
        {
            public string id;
            public string author;
            public string html;
            public string text;
            public int score;
            public string permalink;
            public long timestamp;
            public string title;

            public class Report
            {
                public string reason;
                public int count;
            }
            public Report[] reports;
        }
    }
}
