using System.Globalization;
using System.Windows.Data;
using Hanki.Core.Diagnostics;

namespace Hanki.App.Converters;

public sealed class CompatibilityEnumDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        DelimiterKey.Space => "Space",
        DelimiterKey.Enter => "Enter",
        DelimiterKey.NumpadEnter => "숫자패드 Enter",
        DelimiterKey.Tab => "Tab",
        ManualCheckStatus.NotTested => "테스트하지 않음",
        ManualCheckStatus.Success => "성공",
        ManualCheckStatus.DetectedButInjectionFailed => "감지는 했지만 입력 실패",
        ManualCheckStatus.NoResponse => "아무 반응 없음",
        ManualCheckStatus.NotAvailable => "환경 없음",
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
