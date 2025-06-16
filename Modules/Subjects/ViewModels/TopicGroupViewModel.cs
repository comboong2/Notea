using System.Collections.ObjectModel;
using System.Windows.Input;
using SP.Modules.Subjects.Models;
using SP.ViewModels;

namespace SP.Modules.Subjects.ViewModels
{
    public class TopicGroupViewModel : ViewModelBase
    {
        public string GroupTitle { get; set; } = string.Empty;

        public string ParentSubjectName { get; set; } = string.Empty;

        public ObservableCollection<TopicItem> Topics { get; set; } = new();

        private bool _isExpanded = false;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ICommand ToggleCommand { get; }

        public TopicGroupViewModel()
        {
            ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        // ✅ 분류별 학습 시간 (초 단위)
        private int _totalStudyTime;
        public int TotalStudyTime
        {
            get => _totalStudyTime;
            set => SetProperty(ref _totalStudyTime, value);
        }

        // ✅ 전체 과목 학습 시간 (외부에서 주입)
        private int _subjectTotalTime;
        public void SetSubjectTotalTime(int subjectTime)
        {
            _subjectTotalTime = subjectTime;
            OnPropertyChanged(nameof(ProgressRatio));
        }

        // ✅ 퍼센트 계산 (0.0 ~ 1.0)
        public double ProgressRatio => _subjectTotalTime > 0
            ? (double)TotalStudyTime / _subjectTotalTime
            : 0.0;
    }
}
