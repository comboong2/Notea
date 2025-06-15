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
using System.Windows.Navigation;
using System.Windows.Shapes;
using SP.Modules.Daily.ViewModels;

namespace SP.Modules.Daily.Views
{
    /// <summary>
    /// DailyView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DailyView : UserControl
    {
        public DailyView()
        {
            InitializeComponent();

            var appStartDate = DateTime.Now.Date; // 또는 외부에서 전달받도록 수정
            this.DataContext = new DailyViewModel(appStartDate);
        }
    }
}
