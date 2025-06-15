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
using SP.Modules.Common.ViewModels;
using SP.Modules.Daily.Views;
using SP.Modules.Subjects.Views;
using SP.ViewModels;

namespace SP.Modules.Common.Views
{
    /// <summary>
    /// LeftSidebar.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LeftSidebar : UserControl
    {
        public LeftSidebar()
        {
            InitializeComponent();

            DataContext = new LeftSidebarViewModel("today"); // 또는 "main"
        }
        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Window.GetWindow(this)?.DataContext is MainViewModel vm)
            {
                if (vm.HeaderContent is DailyHeaderView)
                {
                    vm.HeaderContent = new SubjectListPageHeaderView();
                    vm.BodyContent = new SubjectListPageBodyView();
                    vm.SidebarViewModel.SetContext("today");
                }
                else
                {
                    vm.HeaderContent = new DailyHeaderView();
                    vm.BodyContent = new DailyBodyView();
                    vm.SidebarViewModel.SetContext("main");
                }
            }
        }

    }
}
