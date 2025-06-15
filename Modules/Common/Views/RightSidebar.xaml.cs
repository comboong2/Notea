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
using SP.Modules.Common.Helpers;
using SP.Modules.Common.Models;
using SP.Modules.Common.ViewModels;

namespace SP.Modules.Common.Views
{
    /// <summary>
    /// RightSidebar.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RightSidebar : UserControl
    {
        private readonly DatabaseHelper _db = new(); // DB 헬퍼
        public RightSidebar()
        {
            InitializeComponent();
        }
        private void MemoTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is Note note)
            {
                _db.UpdateNote(note); // DB에 수정 반영
            }
        }
       
    }
}
