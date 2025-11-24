using EZHolodotNet.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EZHolodotNet.Views
{
    /// <summary>
    /// _3DPreviewWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ThreeDPreviewWindow
    {
        public CoreProcesserViewModel Processer;
        public ThreeDPreviewWindow(CoreProcesserViewModel mainProcesser)
        {
            InitializeComponent();
            Processer = mainProcesser;
            DataContext = Processer;
            this.Closed += Window_Closed;
        }
        //关闭窗口时释放资源
        private void Window_Closed(object sender, EventArgs e)
        {
            Processer.IsAutoPlay3D = false;
        }

    }
}
