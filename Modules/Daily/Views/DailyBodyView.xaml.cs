using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Daily.ViewModels;

namespace SP.Modules.Daily.Views
{
    /// <summary>
    /// DailyBodyView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DailyBodyView : UserControl
    {
        public DailyBodyView()
        {
            InitializeComponent();
 

            this.Loaded += (s, e) =>
            {
                Console.WriteLine(" Loaded fired");
                if (this.DataContext is DailyBodyViewModel vm)
                {
                    vm.RequestFocusOnInput = () =>
                    {
                        TodoAddBox.Focus();
                        TodoAddBox.SelectAll();
                    };
                }
            };
#if DEBUG
            if (DesignerProperties.GetIsInDesignMode(this)) // 'this'가 UIElement를 참조하는지 확인 (UserControl, Window 등)
            {
                Debug.WriteLine("[디자인 모드] DailyBodyView 생성됨 (디자이너)");
                return; // 디자이너 모드에서는 더 이상 코드 실행을 원치 않을 경우
            }
#endif

            Debug.WriteLine("[런타임] DailyBodyView 생성됨, DataContext: " + (this.DataContext?.GetType().Name ?? "null"));
            Debug.WriteLine(Environment.StackTrace); // 현재 호출한 코드 스택을 출력

        }

        //TextBox가 보일 때 자동 포커스
        private void TodoAddBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (TodoAddBox.IsVisible)
            {
                TodoAddBox.Focus();
            }
            else
            {
                // 포커스 다른 곳으로 넘겨 점선 테두리 제거
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(TodoAddBox), null);
                Keyboard.ClearFocus();
            }
        }

        private void CommentTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                // Window로 포커스 이동 (점선 없음)
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }
    }
}
