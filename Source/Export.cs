
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PaperclipPerfector
{
    public static class Export
    {
        public static void Run()
        {
            var doc = new XDocument(new XElement("posts",
                    Db.Instance.ReadAllPosts(Db.PostState.Posted, int.MaxValue, Db.LimitBehavior.All).Select(post =>
                        new XElement("post",
                            new XElement("author", post.author),
                            new XElement("date", post.creation),
                            new XElement("link", post.link),
                            new XElement("title", post.flavorTitle.Trim('.')),
                            new XElement("body", post.html)
                        )
                    )
                ));

            File.WriteAllText("export.xml", doc.ToString());
        }
    }
}
