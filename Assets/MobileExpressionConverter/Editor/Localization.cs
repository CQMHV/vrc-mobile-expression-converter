using System;
using System.Collections.Generic;
using nadena.dev.ndmf.localization;

namespace MobileExpressionConverter.Editor
{
    internal static class Texts
    {
        private static readonly Dictionary<string, (string Chinese, string Japanese)> Translations =
            new Dictionary<string, (string Chinese, string Japanese)>();
        private static readonly Localizer Localizer = new Localizer("en-US",
            () => new List<(string, Func<string, string>)>
            {
                ("en-US", key => Translations.ContainsKey(key) ? key : null),
                ("zh-Hans", key => Translations.TryGetValue(key, out var value) ? value.Chinese : null),
                ("ja-JP", key => Translations.TryGetValue(key, out var value) ? value.Japanese : null)
            });

        internal static Localizer NdmfLocalizer => Localizer;
        internal static string T(string english, string chinese, string japanese)
        {
            Translations[english] = (chinese, japanese);
            return Localizer.TryGetLocalizedString(english, out var value) ? value : english;
        }
    }
}
