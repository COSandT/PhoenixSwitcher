using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace PhoenixSwitcher.ValueConverters
{

    public class FontSizeForTextConverter : IMultiValueConverter
    {
        public double MinFontSize { get; set; } = 10.0;
        public double MaxFontSize { get; set; } = 72.0;
        public double Multiplier { get; set; } = 0.85;

        private const double AvgCharWidthFactor = 0.6;
        private const double LineHeightFactor = 1.2;

        private const double DefaultFallbackWidth = 640.0;
        private const double DefaultFallbackHeight = 200.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double availableWidth = GetSafeDouble(values, 0, DefaultFallbackWidth);
            double availableHeight = GetSafeDouble(values, 1, DefaultFallbackHeight);
            string text = values != null && values.Length > 2 ? values[2] as string ?? string.Empty : string.Empty;

            if (string.IsNullOrEmpty(text)) return MinFontSize;

            // Trim and compute stats
            text = text.Trim();
            int textLength = Math.Max(1, text.Length);
            int longestWordLength = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length).DefaultIfEmpty(1).Max();

            // Binary search for largest font size that fits
            double low = MinFontSize;
            double high = MaxFontSize;
            double best = MinFontSize;
            for (int i = 0; i < 12 && low <= high; i++)
            {
                double mid = (low + high) / 2.0;
                if (Fits(mid, availableWidth, availableHeight, textLength, longestWordLength))
                {
                    best = mid;
                    low = mid + 0.1;
                }
                else
                {
                    high = mid - 0.1;
                }
            }
            best *= Multiplier;
            return Math.Max(MinFontSize, Math.Min(MaxFontSize, best));
        }

        private bool Fits(double fontSize, double availableWidth, double availableHeight, int textLength, int longestWordLength)
        {
            // Estimate characters per line based on avg char width
            double charPixel = fontSize * AvgCharWidthFactor;
            if (charPixel <= 0.0) return false;

            int charsPerLine = (int)Math.Floor(availableWidth / charPixel);

            // Ensure at least longest word will fit (if not, this font doesn't fit)
            if (charsPerLine < longestWordLength)
            {
                return false;
            }

            // Prevent zero division
            if (charsPerLine <= 0) charsPerLine = 1;

            int lines = (int)Math.Ceiling((double)textLength / charsPerLine);
            double estimatedHeight = lines * fontSize * LineHeightFactor;

            return estimatedHeight <= availableHeight + 0.5;
        }

        private double GetSafeDouble(object[] values, int index, double fallback)
        {
            if (values == null || index >= values.Length) return fallback;
            if (values[index] is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d) || d <= 0.0) return fallback;
                return d;
            }
            return fallback;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
