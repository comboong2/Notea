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

        private int _todayStudyTimeSeconds;
        public int TodayStudyTimeSeconds
        {
            get => _todayStudyTimeSeconds;
            set
            {
                if (SetProperty(ref _todayStudyTimeSeconds, value))
                {
                    OnPropertyChanged(nameof(ProgressRatio));
                    OnPropertyChanged(nameof(StudyTimeTooltip));
                }
            }
        }

        // ✅ 분류별 학습 시간 (초 단위)
        public int TotalStudyTime
        {
            get => TodayStudyTimeSeconds;
            set => TodayStudyTimeSeconds = value;
        }

        private int _parentTodayStudyTimeSeconds;

        public void SetParentTodayStudyTime(int parentTodayTimeSeconds)
        {
            _parentTodayStudyTimeSeconds = parentTodayTimeSeconds;
            OnPropertyChanged(nameof(ProgressRatio));
            OnPropertyChanged(nameof(StudyTimeTooltip));

            System.Diagnostics.Debug.WriteLine($"[TopicGroup] {GroupTitle} 부모 오늘 시간 설정: {parentTodayTimeSeconds}초");
            System.Diagnostics.Debug.WriteLine($"[TopicGroup] {GroupTitle} 업데이트된 ProgressRatio: {ProgressRatio:P2}");
        }

        // ✅ 전체 과목 학습 시간 (외부에서 주입)
        private int _subjectTotalTime;
        public void SetSubjectTotalTime(int subjectTime)
        {
            _subjectTotalTime = subjectTime;
            OnPropertyChanged(nameof(ProgressRatio));
        }

        // ✅ 퍼센트 계산 (0.0 ~ 1.0)
        public double ProgressRatio
        {
            get
            {
                if (_parentTodayStudyTimeSeconds == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[TopicGroup] {GroupTitle} ProgressRatio 계산 실패: 부모의 오늘 학습시간이 0입니다.");
                    return 0.0;
                }

                var ratio = (double)TodayStudyTimeSeconds / _parentTodayStudyTimeSeconds;
                return Math.Min(1.0, ratio); // 100% 이상은 100%로 제한
            }
        }

        // 🆕 학습 시간을 00:00:00 형식으로 표시하는 툴팁
        public string StudyTimeTooltip
        {
            get
            {
                var hours = TotalStudyTime / 3600;
                var minutes = (TotalStudyTime % 3600) / 60;
                var seconds = TotalStudyTime % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2} ({ProgressRatio:P1})";
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