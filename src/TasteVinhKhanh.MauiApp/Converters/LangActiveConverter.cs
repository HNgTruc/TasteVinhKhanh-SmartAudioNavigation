using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TasteVinhKhanh.MauiApp.Converters;

// ── MultiBinding: dùng trong PoiDetailPage.xaml (không hỗ trợ RelativeSource) ──

/// <summary>
/// MultiBinding converter cho BackgroundColor chip ngôn ngữ.
/// values[0] = LanguageCode item, values[1] = CurrentLangCode từ ViewModel.
/// Active → cam mờ, Inactive → trong suốt.
/// </summary>
public class MultiLangBgConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return Colors.Transparent;
        var itemLang = values[0]?.ToString()?.ToLowerInvariant() ?? "";
        var curLang  = values[1]?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == curLang
            ? Color.FromArgb("#30FF6B35")
            : Colors.Transparent;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// MultiBinding converter cho TextColor chip ngôn ngữ.
/// values[0] = LanguageCode item, values[1] = CurrentLangCode từ ViewModel.
/// Active → cam, Inactive → xám.
/// </summary>
public class MultiLangTextColorConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Color.FromArgb("#555555");
        var itemLang = values[0]?.ToString()?.ToLowerInvariant() ?? "";
        var curLang  = values[1]?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == curLang
            ? Color.FromArgb("#FF6B35")
            : Color.FromArgb("#555555");
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// MultiBinding converter cho IsVisible badge ✓ ngôn ngữ.
/// values[0] = LanguageCode item, values[1] = CurrentLangCode từ ViewModel.
/// Hiện ✓ khi active.
/// </summary>
public class MultiLangBadgeVisibleConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        var itemLang = values[0]?.ToString()?.ToLowerInvariant() ?? "";
        var curLang  = values[1]?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == curLang;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Single-binding converters (ConverterParameter không hoạt động trong MAUI) ──

/// <summary>
/// Trả về Color cho BackgroundColor/TextColor chip ngôn ngữ.
/// Dùng với MultiBinding thay vì ConverterParameter.
/// </summary>
public class LangActiveConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        var itemLang = value?.ToString()?.ToLowerInvariant() ?? "";
        var userLang = parameter?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == userLang
            ? Color.FromRgb(255, 107, 53)
            : Color.FromRgb(85, 85, 85);
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Trả về Color cho BackgroundColor card ngôn ngữ khi được chọn.
/// value = SelectedLanguage từ ViewModel
/// parameter = mã ngôn ngữ của card này (vi/en/zh/ko/ja)
/// Trả về nền cam mờ nếu match, trong suốt nếu không.
/// </summary>
public class LangSelectedBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        var curLang  = value?.ToString()?.ToLowerInvariant() ?? "";
        var cardLang = parameter?.ToString()?.ToLowerInvariant() ?? "";
        return curLang == cardLang
            ? Color.FromArgb("#30FF6B35")
            : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Trả về Color cho Border Stroke khi card ngôn ngữ được chọn.
/// </summary>
public class LangSelectedStrokeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        var curLang  = value?.ToString()?.ToLowerInvariant() ?? "";
        var cardLang = parameter?.ToString()?.ToLowerInvariant() ?? "";
        return curLang == cardLang
            ? Color.FromArgb("#FF6B35")
            : Color.FromArgb("#252525");
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Trả về bool IsVisible cho badge ✓ ngôn ngữ.
/// Dùng với MultiBinding thay vì ConverterParameter.
/// </summary>
public class LangBadgeVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        var itemLang = value?.ToString()?.ToLowerInvariant() ?? "";
        var userLang = parameter?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == userLang;
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// MultiBinding converter cho Border Stroke khi chip ngôn ngữ được chọn.
/// Active → cam viền, Inactive → trong suốt.
/// </summary>
public class MultiLangStrokeConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return Color.FromArgb("#252525");
        var itemLang = values[0]?.ToString()?.ToLowerInvariant() ?? "";
        var curLang  = values[1]?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == curLang
            ? Color.FromArgb("#FF6B35")
            : Color.FromArgb("#252525");
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// MultiBinding converter trả về double cho StrokeThickness khi chip được chọn.
/// Active → 1.5, Inactive → 0.
/// </summary>
public class MultiLangStrokeThicknessConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return 0.0;
        var itemLang = values[0]?.ToString()?.ToLowerInvariant() ?? "";
        var curLang  = values[1]?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == curLang ? 1.5 : 0.0;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// MultiBinding converter trả về nền đậm cho card ngôn ngữ khi được chọn.
/// Active → cam đậm, Inactive → nền tối thường.
/// </summary>
public class MultiLangCardBgConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return Color.FromArgb("#1A1A1A");
        var itemLang = values[0]?.ToString()?.ToLowerInvariant() ?? "";
        var curLang  = values[1]?.ToString()?.ToLowerInvariant() ?? "";
        return itemLang == curLang
            ? Color.FromArgb("#30FF6B35")
            : Color.FromArgb("#1A1A1A");
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
