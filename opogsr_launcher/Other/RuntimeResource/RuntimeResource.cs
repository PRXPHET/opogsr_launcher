using opogsr_launcher.Properties;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Globalization;
using System.Resources;

namespace opogsr_launcher.Other.RuntimeResource
{
    public class RuntimeResource : ReactiveObject
    {
        private static readonly ResourceManager _resourceManager = Resources.ResourceManager;

        [Reactive] public string Language { get; private set; } = CultureInfo.CurrentCulture.ThreeLetterISOLanguageName;

        public string GetString(string key)
        {
            string text = _resourceManager.GetString(key, Resources.Culture);

            if (!string.IsNullOrEmpty(text))
                return text;

            Logger.Error("Can't find resource string with key [{0}] for culture [{1}].", key, Resources.Culture);
            return $"[{key}]";
        }

        public string this[string key] => GetString(key);

        public void SetLanguage(string culture)
        {
            Resources.Culture = new CultureInfo(culture);
            Language = Resources.Culture.ThreeLetterISOLanguageName;

            this.RaisePropertyChanged(string.Empty);
        }
    }
}
