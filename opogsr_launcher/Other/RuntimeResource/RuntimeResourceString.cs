using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;

namespace opogsr_launcher.Other.RuntimeResource
{
    public class RuntimeResourceString : ReactiveObject
    {
        [Reactive] public string Key { get; set; }

        public string Text { get; private set; }

        public RuntimeResourceString(string key)
        {
            Key = key;

            this.WhenAnyValue(rs => rs.Key).Subscribe(_ => UpdateText());
            RuntimeResource.Instance.WhenAnyValue(r => r.Language).Subscribe(_ => UpdateText());

            UpdateText();
        }

        private void UpdateText()
        {
            Text = !string.IsNullOrEmpty(Key) ? RuntimeResource.Instance[Key] : "";
            this.RaisePropertyChanged(nameof(Text));
        }
    }
}
