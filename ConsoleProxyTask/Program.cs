using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;

namespace ConsoleProxyTask
{
    class Program
    {
        private const string remoteUrl = "https://www.reddit.com";
        private const string localUrl = "http://localhost:";
        private const int port = 8080;
        private const string ContentType = "text/html";
        private const string hrefAttribute = "href";
        private const string specialCharecters = "™";

        static void Main(string[] args)
        {
            
            HttpListener listener = CreateHttpListener();

            while (true)
            {
                var context = listener.GetContext();

                var targetUrl = context.Request.Url.AbsoluteUri.Replace($"{localUrl}{port}", "");

                HttpClientHandler clientHandler = new HttpClientHandler();
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                var targetResponse = new HttpClient(clientHandler)
                    .GetAsync($"{remoteUrl}{targetUrl}").Result;

                var targetContent = targetResponse.Content.ReadAsStringAsync().Result;
                var modifiedContent = ModifyOnlyHtml(targetContent);

                modifiedContent = ReplaceInternalLinks(modifiedContent);

                HandleResponse(context, targetResponse, modifiedContent);
            }
        }

        private static void HandleResponse(
            HttpListenerContext context, 
            HttpResponseMessage targetResponse, 
            string modifiedContent)
        {
            var response = context.Response;

            var buffer = Encoding.UTF8.GetBytes(modifiedContent);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);

            response.ContentType = targetResponse.Content.Headers.ContentType.ToString();
            foreach (var headerName in targetResponse.Headers)
            {
                response.Headers.Add(headerName.Key, string.Join(",", headerName.Value));
            }
            response.Headers.Add(HttpResponseHeader.ContentType, ContentType);
            response.StatusCode = (int)targetResponse.StatusCode;

            response.Close();
        }

        private static string ReplaceInternalLinks(string modifiedContent)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(modifiedContent);
            foreach (var linkNode in htmlDocument.DocumentNode.Descendants("a"))
            {
                var href = linkNode.GetAttributeValue(hrefAttribute, null);
                if (!string.IsNullOrEmpty(href) && href.StartsWith("/"))
                {
                    linkNode.SetAttributeValue(hrefAttribute, $"{localUrl}{port}{href}");
                }
            }
            modifiedContent = htmlDocument.DocumentNode.OuterHtml;
            return modifiedContent;
        }

        private static HttpListener CreateHttpListener()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"{localUrl}{port}/");
            listener.Start();
            Console.WriteLine($"Listening on port {port}...");
            return listener;
        }

        private static string ModifyOnlyHtml(string targetContent)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(targetContent);

            foreach (HtmlTextNode node in doc.DocumentNode.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Text))
            {
                string text = node.InnerHtml.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length == 6)
                {
                    node.InnerHtml = text + specialCharecters;
                }
            }

            StringBuilder sb = new StringBuilder();
            StringWriter writer = new StringWriter(sb);
            doc.Save(writer);
            return sb.ToString();
        }
    }
}
