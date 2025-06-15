using System.Collections.ObjectModel;
using System.Windows.Input;
using SP.Modules.Common.ViewModels; // TopicGroupViewModel 위치
using SP.ViewModels; // RelayCommand, ViewModelBase 등

namespace SP.Modules.Subjects.ViewModels
{
    public class SubjectGroupViewModel : ViewModelBase
    {
        public string SubjectName { get; set; } = string.Empty;
        public int SubjectId { get; set; }
        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ICommand ToggleCommand { get; }

        public SubjectGroupViewModel()
        {
            ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        // ✅ [1] 과목별 학습시간 (초 단위)
        public int TotalStudyTime { get; set; }

        // ✅ [2] 전체 과목 학습시간 중 이 과목이 차지하는 비율
        private static int _totalAllSubjectsTime = 0; // 모든 과목의 총합
        public static void SetGlobalTotalTime(int total)
        {
            _totalAllSubjectsTime = total;
        }

        public double ProgressRatio => _totalAllSubjectsTime > 0
            ? (double)TotalStudyTime / _totalAllSubjectsTime
            : 0;
    }
}
