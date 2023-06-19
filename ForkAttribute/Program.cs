using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary;
using WikiClientLibrary.Pages;

//https://github.com/CXuesong/WikiClientLibrary/wiki/%5BMediaWiki%5D-Getting-started
namespace ForkAttribute;
partial class Program
{
    const string url = "https://www.aaroads.com/w/api.php";

    static async Task Main(string[] args)
    {
        
        Console.WriteLine("Parsing " + args[0]);

        // A WikiClient has its own CookieContainer.
        var client = new WikiClient
        {
            ClientUserAgent = "AttributionBot"
        };
        // You can create multiple WikiSite instances on the same WikiClient to share the state.
        var site = new WikiSite(client, url);
        // Wait for initialization to complete.
        // Throws error if any.
        await site.Initialization;
        try
        {
            //need a static field defined here or in another file
            await site.LoginAsync("AttributionBot", password);
        }
        catch (WikiClientException ex)
        {
            Console.WriteLine(ex.Message);
            // Add your exception handler for failed login attempt.
        }

        // Do what you want
        Console.WriteLine("you are now logged in!");

        // Open XML file containing exported wiki pages
        var xml = new XmlDocument();
        xml.Load(args[0]);

        // Deserialize the XML file into the MediaWikiType object
        var serializer = new XmlSerializer(typeof(MediaWikiType));
        var nodeReader = new XmlNodeReader(xml);
        var mw = (MediaWikiType)serializer.Deserialize(nodeReader);

        foreach (var page in mw.page)
        {
            string title = page.title;
            Console.WriteLine(title);
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
            //won't work for non-mainspace!
            var wikiPage = new WikiPage(site, "Talk:" + title);
            await wikiPage.RefreshAsync(PageQueryOptions.FetchContent);
            wikiPage.Content += ("{{attribution|date=" + lastRevision + "|editors=" + resultString + "}}");

            await wikiPage.UpdateContentAsync("Provide attribution", minor: false, bot: true);

        }


        // We're done here
        await site.LogoutAsync();
        client.Dispose();        // Or you may use `using` statement.
    }
}