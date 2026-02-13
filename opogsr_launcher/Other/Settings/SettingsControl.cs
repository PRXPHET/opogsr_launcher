
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Globalization;

namespace opogsr_launcher.Other.Settings
{
    public enum SettingType
    {
        User = 0,
        Script = 1,
        Launcher = 2
    }

    internal partial class SettingsControl<T> : ReactiveObject, ISettingsControl
    {
        [Reactive] public partial T CurrentValue { get; protected set; }

        public T DefaultValue { get; protected set; }

        public SettingType Type { get; protected set; }

        public string Command { get; protected set; }

        public string Desc { get; protected set; }

        public SettingsControl(string cmd, SettingType type = SettingType.User)
        {
            Command = cmd;
            Type = type;

            Storage.Add(Command, this);
        }

        public virtual string GetValueAsString() => CurrentValue.ToString();

        public virtual object GetValue() => CurrentValue;

        public virtual void ChangeValue(string val)
        {
            try
            {
                // Sometimes number values ends with dot and nothing after it, converter doesn't like that
                if (val.EndsWith('.') && char.IsDigit(val[^2]))
                    val = val[..^1];

                CurrentValue = (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                Logger.Error("Settings: Error while converting value [{0}] to type [{1}] for command [{2}]", val, typeof(T).ToString(), Command);
            }
        }
    }

    public interface ISettingsControl
    {
        abstract string GetValueAsString();

        abstract object GetValue();

        abstract void ChangeValue(string val);

        string Command { get; }

        SettingType Type { get; }
    }
}
