using SP.Modules.Monthly.Models;
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

namespace SP.Modules.Monthly.Views
{
    /// <summary>
    /// EventDetailWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EventDetailWindow : Window
    {
        public ICalendarPlan Event { get; private set; }
        public bool IsDeleted { get; private set; } = false;

        public EventDetailWindow(ICalendarPlan calendarEvent)
        {
            InitializeComponent();
            Event = calendarEvent;
            DataContext = Event;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true; // 창 닫힘 + 호출부에서 확인 가능
            Close();
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("정말 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                IsDeleted = true;
                DialogResult = true;
                Close();
            }
        }
    }
}