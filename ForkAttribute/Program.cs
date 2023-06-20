using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary;
using WikiClientLibrary.Pages;

//https://github.com/CXuesong/WikiClientLibrary/wiki/%5BMediaWiki%5D-Getting-started
namespace ForkAttribute;

/*
 * <namespaces>
      <namespace key="-2" case="first-letter">Media</namespace>
      <namespace key="-1" case="first-letter">Special</namespace>
      <namespace key="0" case="first-letter" />
      <namespace key="1" case="first-letter">Talk</namespace>
      <namespace key="2" case="first-letter">User</namespace>
      <namespace key="3" case="first-letter">User talk</namespace>
      <namespace key="4" case="first-letter">Wikipedia</namespace>
      <namespace key="5" case="first-letter">Wikipedia talk</namespace>
      <namespace key="6" case="first-letter">File</namespace>
      <namespace key="7" case="first-letter">File talk</namespace>
      <namespace key="8" case="first-letter">MediaWiki</namespace>
      <namespace key="9" case="first-letter">MediaWiki talk</namespace>
      <namespace key="10" case="first-letter">Template</namespace>
      <namespace key="11" case="first-letter">Template talk</namespace>
      <namespace key="12" case="first-letter">Help</namespace>
      <namespace key="13" case="first-letter">Help talk</namespace>
      <namespace key="14" case="first-letter">Category</namespace>
      <namespace key="15" case="first-letter">Category talk</namespace>
      <namespace key="100" case="first-letter">Portal</namespace>
      <namespace key="101" case="first-letter">Portal talk</namespace>
      <namespace key="118" case="first-letter">Draft</namespace>
      <namespace key="119" case="first-letter">Draft talk</namespace>
      <namespace key="710" case="first-letter">TimedText</namespace>
      <namespace key="711" case="first-letter">TimedText talk</namespace>
      <namespace key="828" case="first-letter">Module</namespace>
      <namespace key="829" case="first-letter">Module talk</namespace>
      <namespace key="2300" case="case-sensitive">Gadget</namespace>
      <namespace key="2301" case="case-sensitive">Gadget talk</namespace>
      <namespace key="2302" case="case-sensitive">Gadget definition</namespace>
      <namespace key="2303" case="case-sensitive">Gadget definition talk</namespace>
    </namespaces>
*/
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


            WikiPage wikiPage;
            switch (page.ns)
            {
                case "0": wikiPage = new WikiPage(site, "Talk:" + title);
                    break;
                case "10":
                    wikiPage = new WikiPage(site, "Template talk:" + title.Replace("Template:",""));
                    break;
                case "14":
                    wikiPage = new WikiPage(site, "Category talk:" + title.Replace("Category:", ""));
                    break;
                case "828":
                    wikiPage = new WikiPage(site, "Module talk:" + title.Replace("Module:", ""));
                    break;
                default: throw new Exception ("undefined namespace " + page.ns);
            }
            await wikiPage.RefreshAsync(PageQueryOptions.FetchContent);
            if (wikiPage.Content != null && wikiPage.Content.Contains("{{attribution")) continue; //no duplicates
            wikiPage.Content += ("{{attribution|date=" + lastRevision + "|editors=" + resultString + "}}");

            await wikiPage.UpdateContentAsync("Provide attribution", minor: false, bot: true);

        }


        // We're done here
        await site.LogoutAsync();
        client.Dispose();        // Or you may use `using` statement.
    }
}