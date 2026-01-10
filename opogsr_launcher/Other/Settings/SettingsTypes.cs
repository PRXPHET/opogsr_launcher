global using Token = System.Collections.ObjectModel.ObservableCollection<string>;

using Avalonia.Media;
using opogsr_launcher.Other.RuntimeResource;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace opogsr_launcher.Other.Settings
{
    internal static class Storage
    {
        private static readonly Dictionary<string, ISettingsControl> StorageDict = [];

        public static void Add(string key, ISettingsControl value) => StorageDict.Add(key, value);

        public static ISettingsControl GetInstance(string key) => StorageDict[key];

        public static object GetValue(string key) => StorageDict[key].GetValue();
    }

    internal class SettingsSeparator : ISettingsControl
    {
        private static readonly Dictionary<string, SolidColorBrush> Brushes = new()
        {
            { "orange", new SolidColorBrush(Color.FromRgb(255, 138, 0)) },
            { "red", new SolidColorBrush(Color.FromRgb(255, 0, 0)) },
            { "white", new SolidColorBrush(Color.FromRgb(255, 255, 255)) },
            { "green", new SolidColorBrush(Color.FromRgb(0, 200, 0)) },
            { "yellow", new SolidColorBrush(Color.FromRgb(255, 239, 0)) },
        };

        public SolidColorBrush Brush { get; set; }

        [Reactive] public RuntimeResourceString Str { get; set; }

        internal SettingsSeparator(string text_id, string color = "orange") 
        {
            Str = new RuntimeResourceString(text_id);
            Brush = Brushes[color];
        }

        public string Command { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string? Desc => throw new NotImplementedException();

        public SettingType Type => throw new NotImplementedException();

        public string GetValueAsString() => throw new NotImplementedException();

        public object GetValue() => throw new NotImplementedException();

        public void ChangeValue(string val) => throw new NotImplementedException();
    }

    public class ControlToken
    {
        public RuntimeResourceString DisplayString { get; set; }
        public string Value { get; set; }
    }

    internal class SettingsToken : SettingsControl<ControlToken>
    {
        public ObservableCollection<ControlToken> Values { get; set; } = [];

        internal SettingsToken(string cmd, Token values, string defaultValue, SettingType type = SettingType.User) : base(cmd, type)
        {
            Trace.Assert(values.Contains(defaultValue));

            foreach (string v in values)
                Values.Add(new() { DisplayString = new(v), Value = v });

            CurrentValue = DefaultValue = Values.First(v => v.Value == defaultValue);
        }

        public override string GetValueAsString() => CurrentValue.Value;

        public override object GetValue() => CurrentValue.Value;

        public override void ChangeValue(string val)
        {
            try
            {
                CurrentValue = Values.First(v => v.Value == val);
            }
            catch
            {
                Logger.Error("Value [{0}] not found in token-type command [{1}]", val, Command);
                CurrentValue = DefaultValue;
            }
        }
    }

    internal class SettingsTokenRaw : SettingsControl<string>
    {
        public Token Values { get; set; } = [];

        internal SettingsTokenRaw(string cmd, Token values, string defaultValue, SettingType type = SettingType.User) : base(cmd, type)
        {
            Trace.Assert(values.Contains(defaultValue));

            Values = values;

            CurrentValue = DefaultValue = Values.First(v => v == defaultValue);
        }

        public override string GetValueAsString() => CurrentValue;

        public override void ChangeValue(string val)
        {
            try
            {
                CurrentValue = Values.First(v => v == val);
            }
            catch
            {
                Logger.Error("Value [{0}] not found in token-type command [{1}]", val, Command);
                CurrentValue = DefaultValue;
            }
        }
    }

    internal class SettingsRange<T> : SettingsControl<T>, ISettingsControl where T : struct, IComparable<T>, IConvertible
    {
        public T Min { get; protected set; }

        public T Max { get; protected set; }

        public T Step { get; protected set; }

        public SettingsRange(string cmd, T min, T max, T step, T defaultValue, SettingType type = SettingType.User) : base(cmd, type)
        {
            Trace.Assert(defaultValue.CompareTo(min) >= 0 && defaultValue.CompareTo(max) <= 0);

            Min = min;
            Max = max;
            Step = step;
            CurrentValue = DefaultValue = defaultValue;
        }

        public override void ChangeValue(string val)
        {
            base.ChangeValue(val);

            if (!(CurrentValue.CompareTo(Min) >= 0 && CurrentValue.CompareTo(Max) <= 0))
            {
                Logger.Error("Invalid value for [{0}], reset to default", Command);
                CurrentValue = DefaultValue;
            }
        }
    }

    internal class SettingsBool : SettingsControl<bool>
    {
        internal SettingsBool(string cmd, bool defaultValue, SettingType type = SettingType.User) : base(cmd, type)
        {
            CurrentValue = DefaultValue = defaultValue;
        }

        public override string GetValueAsString()
        {
            if (Type == SettingType.Script || Type == SettingType.Launcher)
                return CurrentValue.ToString().ToLower(CultureInfo.InvariantCulture);
            else
                return CurrentValue ? "on" : "off";
        }

        public override void ChangeValue(string val)
        {
            CurrentValue = (val == "on" || val == "true");
        }
    }
}
