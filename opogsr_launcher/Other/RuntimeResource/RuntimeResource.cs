using opogsr_launcher.Properties;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System.Globalization;
using System.Resources;

namespace opogsr_launcher.Other.RuntimeResource
{
    public sealed partial class RuntimeResource : ReactiveObject
    {
        private static readonly RuntimeResource _instance = new();

        public static RuntimeResource Instance { get { return _instance; } }

        private static readonly ResourceManager _resourceManager = Resources.ResourceManager;

        [Reactive] public partial string Language { get; private set; }

        private RuntimeResource()
        {
            Language = CultureInfo.CurrentCulture.ThreeLetterISOLanguageName;
        }

        public static string GetString(string key)
        {
            string text = _resourceManager.GetString(key, Resources.Culture);

            if (!string.IsNullOrEmpty(text))
                return text;

            return $"[{key}]";
        }

        public static bool ContainsString(string key)
        {
            var set = _resourceManager.GetResourceSet(Resources.Culture, false, true);
            return set != null && set.GetString(key) != null;
        }

        public string this[string key] => GetString(key);

        public void SetLanguage(string culture)
        {
            Resources.Culture = new CultureInfo(culture);
            Language = Resources.Culture.ThreeLetterISOLanguageName;

            Instance.RaisePropertyChanged(string.Empty);
        }
    }
}
