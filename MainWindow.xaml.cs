using System;
using System.Windows;
using System.Windows.Media;
using SP.ViewModels;
using SP.Modules.Common.ViewModels;
using SP.Modules.Common.Views;

namespace SP
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel(); // 메인 뷰모델 연결

            // 앱 종료 이벤트
            this.Closing += MainWindow_Closing;

            // NOTE: Calendar.LoadEvents() 등 초기화 로직은 각 ViewModel이나 View에서 수행하는 게 안전
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // LayoutShell에서 RightSidebar 가져오기
                var layoutShell = this.Content as LayoutShell;
                if (layoutShell != null)
                {
                    var rightSidebar = FindChild<RightSidebar>(layoutShell);
                    if (rightSidebar?.DataContext is RightSidebarViewModel timerVM)
                    {
                        timerVM.EndSession(); // 세션 저장
                        System.Diagnostics.Debug.WriteLine("[MainWindow] 앱 종료 시 타이머 세션 저장 완료");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 앱 종료 시 오류: {ex.Message}");
            }
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}
