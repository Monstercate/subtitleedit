using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Nikse.SubtitleEdit.Controls;

public class SearchableFontAutoCompleteBox : AutoCompleteBox
{
    public const double DefaultFontBoxWidth = 240;
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    private bool _userActivated;
    private bool _showAllForInitialOpen;
    private string _initialOpenText = string.Empty;
    private bool _skipNextPointerRelease;
    private bool _keepOpenForTextBoxClick;

    public event Action<string?>? PreviewFontNameRequested;

    public SearchableFontAutoCompleteBox()
    {
        Width = DefaultFontBoxWidth;
        MinWidth = DefaultFontBoxWidth;
        MaxWidth = DefaultFontBoxWidth;
        MinimumPrefixLength = 0;
        PlaceholderText = Se.Language.General.SearchFontNames;
        ItemTemplate = new FuncDataTemplate<string>((fontName, _) => MakeFontSuggestion(fontName), true);
        TextFilter = FontMatches;

        AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        DropDownOpening += (_, _) => StabilizeDropDownWidth();
        DropDownClosing += OnDropDownClosing;
        DropDownClosed += (_, _) => _keepOpenForTextBoxClick = false;
        TextChanged += (_, _) =>
        {
            if (!string.Equals(Text ?? string.Empty, _initialOpenText, StringComparison.CurrentCulture))
            {
                _showAllForInitialOpen = false;
            }

            OpenDropDown();
        };
    }

    public SearchableFontAutoCompleteBox(object dataContext, string textPropertyName, IEnumerable fontNames)
        : this()
    {
        DataContext = dataContext;
        ItemsSource = fontNames;
        Bind(TextProperty, new Binding(textPropertyName) { Mode = BindingMode.TwoWay });
    }

    private bool FontMatches(string? searchText, string? itemText)
    {
        if (_showAllForInitialOpen && string.Equals(searchText, _initialOpenText, StringComparison.CurrentCulture))
        {
            return true;
        }

        return string.IsNullOrEmpty(searchText) ||
               itemText?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    private Control MakeFontSuggestion(string? fontName)
    {
        var border = new Border
        {
            Width = DefaultFontBoxWidth - 8,
            Height = 18,
            Background = Brushes.Transparent,
            Margin = new Thickness(-5, -4, 0, -4),
            Padding = new Thickness(2, 0, 0, 0),
            Child = new TextBlock
            {
                Text = fontName,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };

        border.PointerEntered += (_, _) => PreviewFontNameRequested?.Invoke(fontName);
        border.AddHandler(InputElement.PointerWheelChangedEvent, (s, e) => UiUtil.ScrollDropDownItemOnPointerWheel(e.Source ?? s, e));
        return border;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_skipNextPointerRelease)
        {
            _skipNextPointerRelease = false;
            return;
        }

        _userActivated = true;
        _showAllForInitialOpen = true;
        _initialOpenText = Text ?? string.Empty;
        if (_keepOpenForTextBoxClick)
        {
            e.Handled = true;
            _keepOpenForTextBoxClick = false;
        }

        OpenDropDown();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsDropDownOpen && e.ClickCount == 1 && IsPointerInsideFontSearchBox(e))
        {
            _keepOpenForTextBoxClick = true;
            e.Handled = true;
            StabilizeDropDownWidth();
            return;
        }

        if (e.ClickCount <= 1)
        {
            return;
        }

        _skipNextPointerRelease = true;
        _userActivated = true;
        _showAllForInitialOpen = true;
        _initialOpenText = Text ?? string.Empty;
        Dispatcher.UIThread.Post(() =>
        {
            this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.SelectAll();
            OpenDropDown();
        });
    }

    private void OnDropDownClosing(object? sender, CancelEventArgs e)
    {
        if (!_keepOpenForTextBoxClick)
        {
            return;
        }

        e.Cancel = true;
        _keepOpenForTextBoxClick = false;
        StabilizeDropDownWidth();
    }

    private bool IsPointerInsideFontSearchBox(PointerEventArgs e)
    {
        return e.Source is Visual visual &&
               (ReferenceEquals(visual, this) || visual.GetVisualAncestors().Contains(this));
    }

    private void OpenDropDown()
    {
        if (!_userActivated)
        {
            return;
        }

        StabilizeDropDownWidth();
        Dispatcher.UIThread.Post(() =>
        {
            IsDropDownOpen = true;
            StabilizeDropDownWidth();
        }, DispatcherPriority.Background);
        DispatcherTimer.RunOnce(() =>
        {
            IsDropDownOpen = true;
            StabilizeDropDownWidth();
        }, TimeSpan.FromMilliseconds(10));
        DispatcherTimer.RunOnce(() =>
        {
            IsDropDownOpen = true;
            StabilizeDropDownWidth();
        }, TimeSpan.FromMilliseconds(50));
    }

    private void StabilizeDropDownWidth()
    {
        TryStabilizeDropDownWidth(12);
    }

    private void TryStabilizeDropDownWidth(int remainingAttempts)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var popup = GetDropDownPopup();
            if (popup == null)
            {
                if (remainingAttempts > 0)
                {
                    DispatcherTimer.RunOnce(() => TryStabilizeDropDownWidth(remainingAttempts - 1), TimeSpan.FromMilliseconds(25));
                }

                return;
            }

            var width = CalculateDropDownWidth();
            popup.PlacementTarget = this;
            popup.Placement = PlacementMode.BottomEdgeAlignedLeft;
            popup.HorizontalOffset = 0;
            popup.Width = width;
            popup.MinWidth = width;
            popup.MaxWidth = width;

            if (popup.Child is Control popupChild)
            {
                popupChild.Width = width;
                popupChild.MinWidth = width;
                popupChild.MaxWidth = width;
            }

            if (remainingAttempts > 0)
            {
                DispatcherTimer.RunOnce(() => TryStabilizeDropDownWidth(remainingAttempts - 1), TimeSpan.FromMilliseconds(25));
            }
        }, DispatcherPriority.Background);
    }

    private Popup? GetDropDownPopup()
    {
        return typeof(AutoCompleteBox)
                   .GetProperty("DropDownPopup", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                   ?.GetValue(this) as Popup ??
               this.GetVisualDescendants().OfType<Popup>()
                   .Concat(this.GetLogicalDescendants().OfType<Popup>())
                   .FirstOrDefault();
    }

    private double CalculateDropDownWidth()
    {
        var width = Bounds.Width;
        if (double.IsNaN(width) || width <= 0)
        {
            width = Math.Max(Width, MinWidth);
        }

        var maxTextWidth = 0.0;
        if (ItemsSource != null)
        {
            var fontFamily = GetValue(TextBlock.FontFamilyProperty);
            var fontSize = GetValue(TextBlock.FontSizeProperty);
            var typeface = new Typeface(fontFamily);
            foreach (var item in ItemsSource.Cast<object?>().Take(500))
            {
                var text = item?.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black);

                maxTextWidth = Math.Max(maxTextWidth, formattedText.Width);
            }
        }

        const double dropDownChromePadding = 56;
        const double maxReasonableDropDownWidth = 560;
        return Math.Min(Math.Max(width, maxTextWidth + dropDownChromePadding), maxReasonableDropDownWidth);
    }
}
