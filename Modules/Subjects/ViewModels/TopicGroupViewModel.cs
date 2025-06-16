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

        // 🆕 체크 상태 추가 - DB 저장 기능 포함
        private bool _isCompleted = false;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (SetProperty(ref _isCompleted, value))
                {
                    // 🆕 체크 상태 변경 시 DB에 저장
                    SaveCheckStateToDatabase();
                }
            }
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

        // 🆕 학습 시간을 00:00:00 형식으로 표시하는 툴팁
        public string StudyTimeTooltip
        {
            get
            {
                var hours = TotalStudyTime / 3600;
                var minutes = (TotalStudyTime % 3600) / 60;
                var seconds = TotalStudyTime % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
        }

        // 🆕 체크 상태를 DB에 저장하는 메소드
        private void SaveCheckStateToDatabase()
        {
            try
            {
                if (!string.IsNullOrEmpty(ParentSubjectName) && !string.IsNullOrEmpty(GroupTitle))
                {
                    var dbHelper = SP.Modules.Common.Helpers.DatabaseHelper.Instance;
                    dbHelper.UpdateDailyTopicGroupCompletion(System.DateTime.Today, ParentSubjectName, GroupTitle, IsCompleted);
                    System.Diagnostics.Debug.WriteLine($"[TopicGroup] 체크 상태 저장: {GroupTitle} = {IsCompleted}");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TopicGroup] 체크 상태 저장 오류: {ex.Message}");
            }
        }
    }
}