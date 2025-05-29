using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;

namespace opogsr_launcher.Other.RuntimeResource
{
    public class RuntimeResourceString : ReactiveObject
    {
        private static RuntimeResource Resource;

        [Reactive] public string Key { get; set; }

        public string Text { get; private set; }

        public RuntimeResourceString(RuntimeResource resource, string key)
        {
            Resource = resource;
            Key = key;

            this.WhenAnyValue(rs => rs.Key).Subscribe(_ => UpdateText());
            Resource.WhenAnyValue(r => r.Language).Subscribe(_ => UpdateText());

            UpdateText();
        }

        private void UpdateText()
        {
            Text = !string.IsNullOrEmpty(Key) ? Resource[Key] : "";
            this.RaisePropertyChanged(nameof(Text));
        }
    }
}
