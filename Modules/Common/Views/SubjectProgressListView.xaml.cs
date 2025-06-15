using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SP.Modules.Common.Models;
using SP.Modules.Daily.ViewModels;

namespace SP.Modules.Common.Views
{
    public partial class SubjectProgressListView : UserControl
    {
        public SubjectProgressListView()
        {
            InitializeComponent();
        }

        public ObservableCollection<SubjectProgressViewModel> Subjects
        {
            get => (ObservableCollection<SubjectProgressViewModel>)GetValue(SubjectsProperty);
            set => SetValue(SubjectsProperty, value);
        }

        public static readonly DependencyProperty SubjectsProperty =
            DependencyProperty.Register("Subjects", typeof(ObservableCollection<SubjectProgressViewModel>), typeof(SubjectProgressListView), new PropertyMetadata(null));
    }
}
