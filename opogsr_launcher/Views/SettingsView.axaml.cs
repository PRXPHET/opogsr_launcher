using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using opogsr_launcher.Other.RuntimeResource;
using opogsr_launcher.Other.Settings;
using System;
using System.Globalization;
using System.Linq;

namespace opogsr_launcher.Views;

public partial class SettingsView : UserControl
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    private bool Initializing { get; set; } = true;

    private void ValueChanged()
    {
        // Changing value on object init also calls that so we should ignore
        if (Initializing)
            return;

        ButtonSave.IsEnabled = true;
    }

    private StackPanel BuildSlider<T>(SettingsRange<T> sn) where T : struct, IComparable<T>, IConvertible
    {
        var stack = new StackPanel();

        var text = new TextBlock()
        {
            FontFamily = "fonts:AppFonts#Bender",
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "null",
        };

        // Format the number to needed amount of places because it gets some weird numbers sometimes
        int digits = sn.Step.ToString(CultureInfo.InvariantCulture).SkipWhile(c => c != '.').Skip(1).Count();

        var binding = new Binding("CurrentValue")
        {
            Source = sn,
            StringFormat = "{0:F" + digits.ToString() + "}"
        };

        text.Bind(TextBlock.TextProperty, binding);

        var sld = new Slider
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Minimum = sn.Min.ToDouble(CultureInfo.InvariantCulture),
            Maximum = sn.Max.ToDouble(CultureInfo.InvariantCulture),
            TickFrequency = sn.Step.ToDouble(CultureInfo.InvariantCulture),
            IsSnapToTickEnabled = true,
            Value = sn.CurrentValue.ToDouble(CultureInfo.InvariantCulture),
        };

        sld.Bind(Slider.ValueProperty, binding);
        sld.GetObservable(Slider.ValueProperty).Subscribe(_ => ValueChanged());

        stack.Children.Add(text);
        stack.Children.Add(sld);

        return stack;
    }

    private void InitializeTab(string header, SettingsControls settings)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        foreach (ISettingsControl st in settings)
        {
            var stGrid = new Grid()
            {
                Background = TransparentBrush // it is required for tooltip hit-testing
            };

            if (st is SettingsSeparator separator)
            {
                var sep = new TextBlock()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                    FontFamily = "fonts:AppFonts#Bender",
                    FontSize = 20,
                    Foreground = separator.Brush
                };

                sep.Bind(TextBlock.TextProperty, new Binding("Str.Text") { Source = separator });

                var border = new Border
                {
                    Child = sep
                };

                stGrid.Children.Add(border);

                stack.Children.Add(stGrid);

                continue;
            }

            stGrid.ColumnDefinitions.Add(new ColumnDefinition(0.5, GridUnitType.Star));
            stGrid.ColumnDefinitions.Add(new ColumnDefinition(0.5, GridUnitType.Star));

            var left = new Border();
            Grid.SetColumn(left, 0);

            var right = new Border();
            Grid.SetColumn(right, 1);

            stGrid.Children.Add(left);
            stGrid.Children.Add(right);

            var textCmdStr = new RuntimeResourceString(st.Command);
            var textBindingCmd = new Binding("Text")
            {
                Source = textCmdStr
            };

            var tooltipStack = new StackPanel();

            var tooltipTextCmd = new TextBlock() 
            { 
                FontSize = 16
            };
            tooltipTextCmd.Bind(TextBlock.TextProperty, textBindingCmd);

            tooltipStack.Children.Add(tooltipTextCmd);

            string desc_id = st.Command + "_desc";

            if (RuntimeResource.ContainsString(desc_id))
            {
                var tooltipTextDesc = new TextBlock();
                tooltipTextDesc.Bind(TextBlock.TextProperty, new Binding("Text")
                {
                    Source = new RuntimeResourceString(desc_id)
                });

                tooltipStack.Children.Add(tooltipTextDesc);
            }

            ToolTip.SetTip(stGrid, tooltipStack);
            ToolTip.SetShowDelay(stGrid, 10);

            var leftText = new TextBlock() 
            {
                FontFamily = "fonts:AppFonts#Bender",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            leftText.Bind(TextBlock.TextProperty, new Binding("Text")
            {
                Source = textCmdStr
            });

            left.Child = leftText;

            switch (st)
            {
                case SettingsTokenRaw str:
                    var cmb_str = new ComboBox
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ItemsSource = str.Values,
                        SelectedValue = str.CurrentValue,
                    };

                    cmb_str.Bind(ComboBox.SelectedValueProperty, new Binding("CurrentValue") { Source = str });
                    cmb_str.GetObservable(ComboBox.SelectedValueProperty).Subscribe(_ => ValueChanged());

                    right.Child = cmb_str;
                    break;
                case SettingsToken stt:
                    var cmb_stt = new ComboBox
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ItemsSource = stt.Values,
                        SelectedValue = stt.CurrentValue,
                        DisplayMemberBinding = new Binding("DisplayString.Text")
                    };

                    cmb_stt.Bind(ComboBox.SelectedValueProperty, new Binding("CurrentValue") { Source = stt });
                    cmb_stt.GetObservable(ComboBox.SelectedValueProperty).Subscribe(_ => ValueChanged());

                    right.Child = cmb_stt;

                    break;
                case SettingsRange<double> snd:
                    right.Child = BuildSlider(snd);
                    break;
                case SettingsRange<int> sni:
                    right.Child = BuildSlider(sni);
                    break;
                case SettingsBool sb:
                    var cb = new CheckBox
                    {
                        Padding = new Thickness(0, 0, 0, 10),
                        IsChecked = sb.CurrentValue,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };

                    cb.Bind(CheckBox.IsCheckedProperty, new Binding("CurrentValue") { Source = sb });
                    cb.GetObservable(CheckBox.IsCheckedProperty).Subscribe(_ => ValueChanged());

                    right.Child = cb;

                    break;
            }

            stack.Children.Add(stGrid);
        }

        var headerText = new TextBlock();
        headerText.Bind(TextBlock.TextProperty, new Binding("Text")
        {
            Source = new RuntimeResourceString(header)
        });

        SettingsTabs.Items.Add(new TabItem
        {
            FontFamily = "fonts:AppFonts#Bender",
            FontSize = 20,
            Header = headerText,
            Content = stack
        });
    }

    public SettingsView()
    {
        Initializing = true;

        InitializeComponent();

        foreach (var page in SettingsPages.Pages)
        {
            InitializeTab(page.Key, page.Value);
        }

        Initializing = false;
    }
}