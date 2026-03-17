using System;
using Geta.Optimizely.ContentTypeIcons.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geta.Optimizely.ContentTypeIcons.Controllers
{
    [Route(Constants.RouteTemplate)]
    public class ContentTypeIconController : Controller
    {
        private readonly IContentTypeIconService _contentTypeIconService;

        public ContentTypeIconController(IContentTypeIconService contentTypeIconService)
        {
            _contentTypeIconService = contentTypeIconService ?? throw new ArgumentNullException(nameof(contentTypeIconService));
        }

        [Authorize(Policy = Constants.AuthorizationPolicy)]
        public ActionResult Index(ContentTypeIconSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!CheckValidFormatHtmlColor(settings.BackgroundColor) || !CheckValidFormatHtmlColor(settings.ForegroundColor))
            {
                throw new InvalidOperationException("Unknown foreground or background color");
            }

            var image = _contentTypeIconService.LoadIconImage(settings);
            if (image == null)
            {
                throw new InvalidOperationException("Unable to load icon image.");
            }

            return File(image, "image/png");
        }

        internal static bool CheckValidFormatHtmlColor(string inputColor)
        {
            return HtmlColorParser.TryParse(inputColor, out _);
        }
    }
}
