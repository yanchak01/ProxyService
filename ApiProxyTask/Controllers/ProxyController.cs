using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Services.Proxy;
using System;
using System.Threading.Tasks;

namespace ApiProxyTask.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProxyController : ControllerBase
    {
        private readonly IProxyService _proxyService;
        private readonly IConfiguration _configuration;
        public ProxyController(
            IProxyService proxyService,
            IConfiguration configuration)
        {
            _proxyService = proxyService;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("{*args}")]
        public async Task<IActionResult> GetParsedText(string args)
        {
            var normalizedArgs = args?.Replace("%2F", "/").ToLower();
            var targetUrl = $"{ _configuration.GetSection("TargetUrl").Value}/{normalizedArgs}";
            var newUrl = $"{Request.Scheme}://{Request.Host}/{ControllerContext.ActionDescriptor.ControllerName}";
            var result = await _proxyService.HandleResponseAsync(targetUrl, newUrl);
            var page = await result.Content.ReadAsStringAsync();
            return Content(page,"text/html");
        }
    }
}
