using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SkiaSharp;

namespace Geta.Optimizely.ContentTypeIcons
{
    internal static class HtmlColorParser
    {
        private static readonly Lazy<IReadOnlyDictionary<string, SKColor>> NamedColors = new Lazy<IReadOnlyDictionary<string, SKColor>>(
            () => typeof(SKColors)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.PropertyType == typeof(SKColor))
                .ToDictionary(x => x.Name, x => (SKColor)x.GetValue(null), StringComparer.OrdinalIgnoreCase));

        public static bool TryParse(string input, out SKColor color)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                color = default;
                return false;
            }

            if (SKColor.TryParse(input, out color))
            {
                return true;
            }

            return NamedColors.Value.TryGetValue(input, out color);
        }
    }
}
