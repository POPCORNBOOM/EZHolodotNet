using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EZHolodotNet.Controls
{
    /// <summary>
    /// SliderPanel.xaml 的交互逻辑
    /// </summary>
    public partial class SliderPanel : UserControl
    {
        public SliderPanel()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateThumbPosition();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbPosition();
        }

        #region Dependency Properties

        public static readonly DependencyProperty XValueProperty =
            DependencyProperty.Register(
                nameof(XValue),
                typeof(double),
                typeof(SliderPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValuePropertyChanged));

        public double XValue
        {
            get => (double)GetValue(XValueProperty);
            set => SetValue(XValueProperty, value);
        }

        public static readonly DependencyProperty YValueProperty =
            DependencyProperty.Register(
                nameof(YValue),
                typeof(double),
                typeof(SliderPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValuePropertyChanged));

        public double YValue
        {
            get => (double)GetValue(YValueProperty);
            set => SetValue(YValueProperty, value);
        }

        public static readonly DependencyProperty MinimumXProperty =
            DependencyProperty.Register(nameof(MinimumX), typeof(double), typeof(SliderPanel), new PropertyMetadata(0.0));

        public double MinimumX
        {
            get => (double)GetValue(MinimumXProperty);
            set => SetValue(MinimumXProperty, value);
        }

        public static readonly DependencyProperty MaximumXProperty =
            DependencyProperty.Register(nameof(MaximumX), typeof(double), typeof(SliderPanel), new PropertyMetadata(100.0));

        public double MaximumX
        {
            get => (double)GetValue(MaximumXProperty);
            set => SetValue(MaximumXProperty, value);
        }

        public static readonly DependencyProperty MinimumYProperty =
            DependencyProperty.Register(nameof(MinimumY), typeof(double), typeof(SliderPanel), new PropertyMetadata(0.0));

        public double MinimumY
        {
            get => (double)GetValue(MinimumYProperty);
            set => SetValue(MinimumYProperty, value);
        }

        public static readonly DependencyProperty MaximumYProperty =
            DependencyProperty.Register(nameof(MaximumY), typeof(double), typeof(SliderPanel), new PropertyMetadata(100.0));

        public double MaximumY
        {
            get => (double)GetValue(MaximumYProperty);
            set => SetValue(MaximumYProperty, value);
        }

        private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SliderPanel panel)
                panel.UpdateThumbPosition();
        }

        #endregion

        #region Thumb / position helpers

        private bool _isDragging = false;

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
            PART_Thumb.CaptureMouse();
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            PART_Thumb.ReleaseMouseCapture();
            UpdateThumbPosition();
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double trackWidth = Math.Max(0.0, PART_Track.ActualWidth - PART_Thumb.ActualWidth);
            double trackHeight = Math.Max(0.0, PART_Track.ActualHeight - PART_Thumb.ActualHeight);

            double left = Canvas.GetLeft(PART_Thumb) + e.HorizontalChange;
            double top = Canvas.GetTop(PART_Thumb) + e.VerticalChange;

            left = Math.Max(0, Math.Min(trackWidth, left));
            top = Math.Max(0, Math.Min(trackHeight, top));

            double newX = MinimumX + (left / Math.Max(1.0, trackWidth)) * (MaximumX - MinimumX);
            double newY = MinimumY + (top / Math.Max(1.0, trackHeight)) * (MaximumY - MinimumY);

            XValue = Coerce(newX, MinimumX, MaximumX);
            YValue = Coerce(newY, MinimumY, MaximumY);

            UpdateThumbPosition();
        }

        private void Track_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pt = e.GetPosition(PART_Track);

            double trackWidth = Math.Max(0.0, PART_Track.ActualWidth - PART_Thumb.ActualWidth);
            double trackHeight = Math.Max(0.0, PART_Track.ActualHeight - PART_Thumb.ActualHeight);

            double left = pt.X - PART_Thumb.ActualWidth / 2.0;
            double top = pt.Y - PART_Thumb.ActualHeight / 2.0;

            left = Math.Max(0, Math.Min(trackWidth, left));
            top = Math.Max(0, Math.Min(trackHeight, top));

            double newX = MinimumX + (left / Math.Max(1.0, trackWidth)) * (MaximumX - MinimumX);
            double newY = MinimumY + (top / Math.Max(1.0, trackHeight)) * (MaximumY - MinimumY);

            XValue = Coerce(newX, MinimumX, MaximumX);
            YValue = Coerce(newY, MinimumY, MaximumY);

            UpdateThumbPosition();

            PART_Thumb.Focus();
            e.Handled = true;
        }

        private static double Coerce(double value, double min, double max)
        {
            if (double.IsNaN(value)) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void UpdateThumbPosition()
        {
            if (PART_Track == null || PART_Thumb == null) return;

            double trackWidth = Math.Max(0.0, PART_Track.ActualWidth - PART_Thumb.ActualWidth);
            double trackHeight = Math.Max(0.0, PART_Track.ActualHeight - PART_Thumb.ActualHeight);

            double rangeX = Math.Max(1.0, MaximumX - MinimumX);
            double rangeY = Math.Max(1.0, MaximumY - MinimumY);

            double normalizedX = (XValue - MinimumX) / rangeX;
            double normalizedY = (YValue - MinimumY) / rangeY;

            normalizedX = Math.Max(0, Math.Min(1, normalizedX));
            normalizedY = Math.Max(0, Math.Min(1, normalizedY));

            double left = normalizedX * trackWidth;
            double top = normalizedY * trackHeight;

            Canvas.SetLeft(PART_Thumb, left);
            Canvas.SetTop(PART_Thumb, top);
        }

        #endregion
    }
}
