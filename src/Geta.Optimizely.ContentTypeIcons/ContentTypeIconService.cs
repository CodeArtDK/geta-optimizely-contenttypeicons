using System;
using System.IO;
using EPiServer.Framework.Internal;
using EPiServer.Shell;
using Geta.Optimizely.ContentTypeIcons.Infrastructure.Configuration;
using Geta.Optimizely.ContentTypeIcons.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Geta.Optimizely.ContentTypeIcons
{
    public class ContentTypeIconService : IContentTypeIconService
    {
        private readonly IFileProvider _fileProvider;
        private readonly IPhysicalPathResolver _physicalPathResolver;
        private readonly IMemoryCache _cache;
        private readonly ContentTypeIconOptions _configuration;

        public ContentTypeIconService(
            IOptions<ContentTypeIconOptions> options,
            IFileProvider fileProvider,
            IPhysicalPathResolver physicalPathResolver,
            IMemoryCache cache)
        {
            _fileProvider = fileProvider;
            _physicalPathResolver = physicalPathResolver;
            _cache = cache;
            _configuration = options.Value;
        }

        /// <summary>
        /// Loads or creates a icon using the given settings
        /// </summary>
        /// <param name="settings">The ContentTypeIconSettings parameter</param>
        /// <returns></returns>
        public virtual byte[] LoadIconImage(ContentTypeIconSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var fileName = settings.GetFileName(".png");
            var cachePath = GetFileFullPath(fileName);

            if (File.Exists(cachePath))
            {
                return File.ReadAllBytes(cachePath);
            }

            var image = GenerateImage(settings);
            File.WriteAllBytes(cachePath, image);
            return image;
        }

        protected virtual byte[] GenerateImage(ContentTypeIconSettings settings)
        {
            using var typeface = settings.UseEmbeddedFont
                ? LoadEmbeddedTypeface(settings.EmbeddedFont)
                : LoadTypefaceFromDisk(settings.CustomFontName);

            if (!HtmlColorParser.TryParse(settings.BackgroundColor, out var background))
            {
                throw new InvalidOperationException($"Unable to parse background color '{settings.BackgroundColor}'.");
            }

            if (!HtmlColorParser.TryParse(settings.ForegroundColor, out var foreground))
            {
                throw new InvalidOperationException($"Unable to parse foreground color '{settings.ForegroundColor}'.");
            }

            using var image = new SKBitmap(settings.Width, settings.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(image);
            canvas.Clear(background);

            using var paint = new SKPaint
            {
                Color = foreground,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                TextSize = settings.FontSize,
                Typeface = typeface
            };

            canvas.Save();
            ApplyTransformation(canvas, settings);

            var metrics = paint.FontMetrics;
            var x = settings.Width / 2f;
            var y = settings.Height / 2f - ((metrics.Ascent + metrics.Descent) / 2f);
            var character = char.ConvertFromUtf32(settings.Character);
            canvas.DrawText(character, x, y, paint);
            canvas.Restore();

            using var renderedImage = SKImage.FromBitmap(image);
            using var data = renderedImage.Encode(SKEncodedImageFormat.Png, 100);
            if (data == null)
            {
                throw new InvalidOperationException("Unable to encode icon image as PNG.");
            }

            return data.ToArray();
        }

        protected virtual SKTypeface LoadEmbeddedTypeface(string fileName)
        {
            var cacheKey = $"geta.fontawesome.embedded.typeface.{fileName}";
            if (_cache.TryGetValue(cacheKey, out byte[] fontData) && fontData != null)
            {
                return CreateTypeface(fontData, fileName);
            }

            try
            {
                var path = Paths.ToClientResource(Constants.ModuleName, $"ClientResources/{fileName}");
                var file = _fileProvider.GetFileInfo(path);
                using var fontStream = file.CreateReadStream();
                using var memoryStream = new MemoryStream();
                fontStream.CopyTo(memoryStream);
                fontData = memoryStream.ToArray();

                _cache.Set(cacheKey, fontData, DateTimeOffset.Now.AddMinutes(5));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to load font {fileName} from EmbeddedResource", ex);
            }

            return CreateTypeface(fontData, fileName);
        }

        protected virtual SKTypeface LoadTypefaceFromDisk(string fileName)
        {
            var customFontFolder = _configuration.CustomFontPath;
            var fontPath = $"{customFontFolder}{fileName}";

            var rebased = _physicalPathResolver.Rebase(fontPath);

            try
            {
                var typeface = SKTypeface.FromFile(rebased);
                if (typeface == null)
                {
                    throw new InvalidOperationException($"Unable to create typeface from path {rebased}");
                }

                return typeface;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to load custom font from path {fontPath}", ex);
            }
        }

        protected virtual string GetFileFullPath(string fileName)
        {
            var rootPath = _configuration.CachePath;
            return _physicalPathResolver.Rebase(rootPath + fileName);
        }

        private static SKTypeface CreateTypeface(byte[] fontData, string fileName)
        {
            var typeface = SKTypeface.FromStream(new MemoryStream(fontData, writable: false));

            if (typeface == null)
            {
                throw new InvalidOperationException($"Unable to create typeface from font {fileName}");
            }

            return typeface;
        }

        private static void ApplyTransformation(SKCanvas canvas, ContentTypeIconSettings settings)
        {
            var centerX = settings.Width / 2f;
            var centerY = settings.Height / 2f;

            canvas.Translate(centerX, centerY);

            switch (settings.Rotate)
            {
                case Rotations.Rotate90:
                case Rotations.Rotate180:
                case Rotations.Rotate270:
                    canvas.RotateDegrees((float)settings.Rotate);
                    break;
                case Rotations.FlipHorizontal:
                    canvas.Scale(-1f, 1f);
                    break;
                case Rotations.FlipVertical:
                    canvas.Scale(1f, -1f);
                    break;
            }

            canvas.Translate(-centerX, -centerY);
        }
    }
}
