using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary;
using WikiClientLibrary.Pages;
using System.ComponentModel.Design;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;

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

    static WikiClient client;
    static WikiSite site;
    static bool IMPORT = true;
    static bool DRAFT = false;

    const string url = "https://wiki.aaroads.com/w/api.php";

    static async Task Main(string[] args)
    {
        
        Console.WriteLine("Parsing " + args[0]);

        await login();

        // Open XML file containing exported wiki pages
        var xml = new XmlDocument();
        xml.Load(args[0]);

        // Deserialize the XML file into the MediaWikiType object
        var serializer = new XmlSerializer(typeof(MediaWikiType));
        var nodeReader = new XmlNodeReader(xml);
        var mw = (MediaWikiType)serializer.Deserialize(nodeReader);
        int errors = 0;
        int warnings = 0;
        int pagesDone = 0;
        foreach (var page in mw.page)
        {
            if (page.ns == "100" || page.ns == "118")
            {
                Console.WriteLine("Skipping " + page.title);
                pagesDone++;
                continue;
            }
            string draft = DRAFT ? "Draft:" : "";
            string title = draft + page.title.Replace("Wikipedia:", "AARoads:");
            Console.WriteLine(title);
            List<string> names = new List<string>();
            List<string> ipNames = new List<string>();

            string lastRevision = "";

            bool isRedirect = false;
            bool pastHistory = false;
            string redirectTarget = "";

            if (page.Items.Count() == 1000 && mw.page.Length > 1)
            {
                warnings++;
                Console.WriteLine("was exactly 1000 revs, skipping, must redo!");
                continue;
            }
            for (int i = 0; i < page.Items.Count(); i++)
            {
                
                var revision = page.Items[i];
                if (revision is RevisionType)
                {
                    RevisionType revisionType = (RevisionType)revision;
                    if (revisionType == null)
                        continue;

                    if (revisionType != null && revisionType.contributor != null)
                    {
                        if (revisionType.contributor.ip != null && revisionType.contributor.ip.Length > 0)
                        {
                            if (!ipNames.Contains(revisionType.contributor.ip))
                            {
                                ipNames.Add(revisionType.contributor.ip);
                            }
                        }
                        else
                        {
                            string user = revisionType.contributor.username;
                            if (user != null && !names.Contains(user))
                            {
                                names.Add(user);
                            }
                        }
                        lastRevision = revisionType.timestamp.ToString("yyyy-MM-dd");
                    }
                    var revText = revisionType.text.Value;
                    if (page.redirect != null && page.redirect.title != null && page.redirect.title.Length > 0)
                    {
                        isRedirect = true;
                        redirectTarget = page.redirect.title;
                    }
                    if (revText != null && !revText.ToLower().Contains("#redirect")) //if there's any rev that doesn't have the redirect
                    {
                        pastHistory = true;
                    }

                    if (i == page.Items.Count() - 1)
                    {

                        if (ipNames.Count > 0)
                            names.Add("Anonymous editors: " + ipNames.Count);

                        //current rev: import

                        if (IMPORT)
                        {
                            try
                            {
                                await importPage(title, revText, isRedirect, revisionType);

                            }
                            catch (Exception ex)
                            {

                                Console.WriteLine(ex.ToString());
                                Thread.Sleep(15 * 1000);
                                Console.WriteLine("retrying");
                                try
                                {
                                    await importPage(title, revText, isRedirect, revisionType);
                                }
                                catch (Exception ex2) {
                                    Console.WriteLine(ex2.ToString());
                                    Thread.Sleep(10 * 1000);
                                    errors++;
                                }
                            }
                        }
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

            if (isRedirect && page.ns == "4") continue; //skip project space redirects
            if (isRedirect && pastHistory)
                title = redirectTarget; //go to the redirect target instead

            WikiPage wikiPage;
            if (DRAFT)
                wikiPage = new WikiPage(site, title.Replace("Draft:", "Draft talk:"));
            else
            {
                switch (page.ns)
                {
                    case "0":
                        wikiPage = new WikiPage(site, "Talk:" + title);
                        break;
                    case "12":
                        wikiPage = new WikiPage(site, "Help talk:" + title);
                        break;
                    case "4":
                        wikiPage = new WikiPage(site, title.Replace("AARoads:", "AARoads talk:"));
                        break;
                    case "10":
                        wikiPage = new WikiPage(site, "Template talk:" + title.Replace("Template:", ""));
                        break;
                    case "14":
                        wikiPage = new WikiPage(site, "Category talk:" + title.Replace("Category:", ""));
                        break;
                    case "828":
                        wikiPage = new WikiPage(site, "Module talk:" + title.Replace("Module:", ""));
                        break;
                    case "100": case "118": continue; //don't care
                    default: throw new Exception("undefined namespace " + page.ns);
                }
            }
            Console.WriteLine("Retrieving talk page");
            await wikiPage.RefreshAsync(PageQueryOptions.FetchContent);

            string content = "";
            if (isRedirect && pastHistory)
            {
                content += "{{attribution|date=" + lastRevision + "|editors=" + resultString + "|redirect=" + page.title;
            }
            else if (wikiPage.Content != null && wikiPage.Content.Contains("{{attribution") && /*!wikiPage.Content.Contains("redirect=yes") &&*/
                wikiPage.Content.Contains("main=yes"))
            {
                content += "{{attribution|date=" + lastRevision + "|editors=" + resultString;
                pagesDone++;
                continue; //no duplicates
            }
            else if (isRedirect && !pastHistory)
            {
                content += "{{attribution|date=" + lastRevision + "|editors=" + resultString;
                pagesDone++;
                continue; //no useless templates
            }
            else
            {
                if (args.Length > 1)
                {
                    content += "{{Talk header|\n{{Banner/" + args[1] + "}}\n}}\n";
                }
                else
                {
                    content += "{{Talk header\n}}\n";
                }
                
                content += "{{attribution|date=" + lastRevision + "|editors=" + resultString;
                content += "|main=yes"; //does nothing but just for ID
            }
            wikiPage.Content += (content + "}}\n");

            
            bool success = false;
            while (!success)
            {
                try
                {
                    Console.WriteLine("Providing attribution");
                    await wikiPage.UpdateContentAsync("Provide attribution", minor: false, bot: true);
                    success = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed, retrying");
                }
            }

            pagesDone++;
            Console.WriteLine((mw.page.Count() - pagesDone) + " items left.");
        }

        await logout();
        Console.WriteLine("");
        Console.WriteLine("There were " + errors + " errors.");
        Console.WriteLine("There were " + warnings + " warnings.");
        Console.Beep();
    }

    async static Task importPage(string title, string revText, bool isRedirect, RevisionType revisionType)
    {
        WikiPage importDest = new WikiPage(site, title);

        await importDest.RefreshAsync(PageQueryOptions.FetchContent);

        if (importDest.Content == revText)
            Console.WriteLine("Import is equal to existing content, skipping");
        else if (importDest.Content != null && importDest.Content.Length > 2)
            Console.WriteLine("Import has content already, skipping, will still attribute, please review");
            else
        {

            importDest.Content = revText; //blank out the content

            Console.WriteLine("Started import at " + DateTime.Now);
            int timeout = 1000 * 180;
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var task = importDest.UpdateContentAsync("Import from [[:w:en:Special:Diff/" + revisionType.id + "]]", minor: false, bot: false); //don't invoke the bot flag
                if (await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token)) == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    await task; //throw exceptions

                    Console.WriteLine("Saved to wiki at " + DateTime.Now + ", now sleeping");
                    if (isRedirect) Thread.Sleep(4 * 1000);
                    else Thread.Sleep(10 * 1000);
                }
                else
                {
                    Console.WriteLine("Timed out at " + DateTime.Now + "");
                    await importDest.RefreshAsync(PageQueryOptions.FetchContent);
                    if (importDest.Content == revText)
                    {
                        Console.WriteLine("Import is equal to existing content, probably was okay");
                        await logout(); //reset client
                        await login();
                    }
                    else
                        throw new Exception("Import failed!");
                }
            }
        }
    }

    async static     Task login()
    {
        // A WikiClient has its own CookieContainer.
        client = new WikiClient
        {
            ClientUserAgent = "AttributionBot"
        };
        client.Timeout = new TimeSpan(0, 0, 180); //seconds

        // You can create multiple WikiSite instances on the same WikiClient to share the state.
        site = new WikiSite(client, url);

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
    }

    async static    Task logout()
    {
        // We're done here
        await site.LogoutAsync();
        client.Dispose();        // Or you may use `using` statement.
        Console.WriteLine("Logged out");
    }
}