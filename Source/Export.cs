
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PaperclipPerfector
{
    public static class Export
    {
        public static void Run()
        {
            Directory.CreateDirectory("export");

            foreach (var post in Db.Instance.ReadAllPosts(Db.PostState.Posted, int.MaxValue, Db.LimitBehavior.All))
            {
                var massagedXml = post.html.Replace("&euro;", "€").Replace("&mdash;", "—").Replace("&ndash;", "–").Replace("<hr>", "<hr />").Replace("<hr /></hr>", "<hr />");
                var doc = new XDocument(
                    new XElement("Post",
                            new XElement("author", post.author),
                            new XElement("date", post.creation),
                            new XElement("link", post.link),
                            new XElement("title", post.flavorTitle.Trim('.')),
                            new XElement("body", XElement.Parse($"<div>{massagedXml}</div>"))
                        )
                    );
                File.WriteAllText($"export/{post.author}-{post.creation.ToString("yyyyMMddHHmmss")}.xml", doc.ToString());
            }
        }
    }
}
