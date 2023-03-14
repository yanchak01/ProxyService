using HtmlAgilityPack;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Services.Proxy
{
    public class ProxyService : IProxyService
    {
        private const string hrefAttribute = "href";
        private const string specialCharecters = "™";

        private readonly HttpClient _httpClient;

        public ProxyService()
        {
            _httpClient = new HttpClient(InitializeClientHandler());
        }

        public async Task<HttpResponseMessage> HandleResponseAsync(string targetUrl, string newUrl)
        {
            var targetResponse = await GetHttpResponseAsync(targetUrl);

            var targetContent = await targetResponse.Content.ReadAsStringAsync();

            string modifiedHtml = ReplaceInternalLinks(newUrl, targetContent);
            modifiedHtml = ModifyOnlyHtml(modifiedHtml);

            return HandleResponse(targetResponse, modifiedHtml);
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
                    node.InnerHtml = text + "™";
                }
            }

            StringBuilder sb = new StringBuilder();
            StringWriter writer = new StringWriter(sb);
            doc.Save(writer);
            return sb.ToString();
        }

        private static string ReplaceInternalLinks(string newUri, string modifiedContent)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(modifiedContent);
            foreach (var linkNode in htmlDocument.DocumentNode.Descendants("a"))
            {
                var href = linkNode.GetAttributeValue(hrefAttribute, null);
                if (!string.IsNullOrEmpty(href))
                {
                    if (href.StartsWith("/"))
                    {
                        linkNode.SetAttributeValue(hrefAttribute, $"{newUri}{href}");
                    }
                }
            }
            modifiedContent = htmlDocument.DocumentNode.OuterHtml;
            return modifiedContent;
        }

        private HttpResponseMessage HandleResponse(
            HttpResponseMessage targetResponse,
            string modifiedContent)
        {
            var response = new HttpResponseMessage();

            response.Content = new StringContent(modifiedContent);

            foreach (var headerName in targetResponse.Headers)
            {
                response.Headers.Add(headerName.Key, string.Join(",", headerName.Value));
            }
            response.StatusCode = targetResponse.StatusCode;

            return response;
        }

        private HttpClientHandler InitializeClientHandler()
        {
            HttpClientHandler clientHandler = new HttpClientHandler();
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            return new HttpClientHandler();
        }
       
        private async Task<HttpResponseMessage> GetHttpResponseAsync(string targetUrl)
        {
            try
            {
                var response = await _httpClient
                    .GetAsync(targetUrl);

                return response;
            }
            catch
            {
                throw;
            }
        } 
    }

    public interface IProxyService
    {
        Task<HttpResponseMessage> HandleResponseAsync(string targetUrl, string newUrl);
    }
}
