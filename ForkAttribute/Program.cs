using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace ForkAttribute;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Parsing " + args[0]);

        // Open XML file containing exported wiki pages
        var xml = new XmlDocument();
        xml.Load(args[0]);

        // Deserialize the XML file into the MediaWikiType object
        var serializer = new XmlSerializer(typeof(MediaWikiType));
        var nodeReader = new XmlNodeReader(xml);
        var mw = (MediaWikiType)serializer.Deserialize(nodeReader);

        var writer = new StreamWriter("output.txt");

        foreach (var page in mw.page)
        {
            string title = page.title;
            Console.WriteLine(title);
            writer.WriteLine("Title: " + title);
            List<string> names = new List<string>();

            string lastRevision = "";

            foreach (var revision in page.Items)
            {
                if (revision is RevisionType)
                {
                    RevisionType revisionType = (RevisionType)revision;

                    if (revisionType.contributor != null)
                    {
                        string user = revisionType.contributor.username ?? revisionType.contributor.ip;
                        if (user != null && !names.Contains(user))
                        {
                            names.Add(user);
                        }
                        lastRevision = revisionType.timestamp.ToString("yyyyMMdd");
                    }
                }
            }

            string resultString = "";
            bool first = true;
            foreach (var name in names)
            {
                if (!first)
                {
                    resultString += ", ";

                }
                resultString += name;
                first = false;
            }
            writer.WriteLine("{{attribution|date=" + lastRevision + "|editors=" + resultString + "}}");

        }

        // serialize the updates back to the same file
        
        writer.Close();
    }
}