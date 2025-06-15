using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;
using SP.ViewModels;

namespace SP.Modules.Daily.ViewModels
{
    public class SubjectProgressViewModel : ViewModelBase
    {
        public string SubjectName { get; set; }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string Tooltip => $"{(int)(Progress * 100)}%";

        public double ProgressWidth => 200 * Progress; // 바 너비 계산용

        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();
    }
}
