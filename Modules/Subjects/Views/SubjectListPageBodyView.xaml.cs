﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Subjects.ViewModels;

namespace SP.Modules.Subjects.Views
{
    /// <summary>
    /// SubjectListPageBodyView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SubjectListPageBodyView : UserControl
    {
        public SubjectListPageBodyView()
        {
            InitializeComponent();
            this.DataContext = new SubjectListPageViewModel(); // ViewModel 명시적 연결
        }

        private void SubjectAddBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (SubjectAddBox.IsVisible)
            {
                SubjectAddBox.Focus();
            }
            else
            {
                // 포커스 다른 곳으로 넘겨 점선 테두리 제거
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(SubjectAddBox), null);
                Keyboard.ClearFocus();
            }
        }


    }

}
