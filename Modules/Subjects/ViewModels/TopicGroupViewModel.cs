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

        // ✅ 체크 상태 추가 - DB 저장 기능 포함
        private bool _isCompleted = false;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (SetProperty(ref _isCompleted, value))
                {
                    SaveCheckStateToDatabase();
                }
            }
        }

        public ICommand ToggleCommand { get; }

        public TopicGroupViewModel()
        {
            ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        // ✅ 모든 시간 관련 프로퍼티를 초단위로 수정
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
                    OnPropertyChanged(nameof(StudyTimeText));
                    OnPropertyChanged(nameof(TotalStudyTime)); // 호환성
                    OnPropertyChanged(nameof(TotalStudyTimeSeconds)); // 메인 프로퍼티
                }
            }
        }

        // ✅ 호환성을 위한 프로퍼티 (기존 분류별 학습 시간)
        public int TotalStudyTime
        {
            get => TodayStudyTimeSeconds;
            set => TodayStudyTimeSeconds = value;
        }

        // ✅ 메인 프로퍼티: 초단위 분류별 학습시간
        public int TotalStudyTimeSeconds
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

        // ✅ 전체 과목 학습 시간 (외부에서 주입) - 초단위
        private int _subjectTotalTimeSeconds;
        public void SetSubjectTotalTime(int subjectTimeSeconds)
        {
            _subjectTotalTimeSeconds = subjectTimeSeconds;
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

        // ✅ 학습 시간을 00:00:00 형식으로 표시하는 텍스트들
        public string StudyTimeText
        {
            get
            {
                var hours = TotalStudyTimeSeconds / 3600;
                var minutes = (TotalStudyTimeSeconds % 3600) / 60;
                var seconds = TotalStudyTimeSeconds % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
        }

        public string StudyTimeTooltip
        {
            get
            {
                var hours = TotalStudyTimeSeconds / 3600;
                var minutes = (TotalStudyTimeSeconds % 3600) / 60;
                var seconds = TotalStudyTimeSeconds % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2} ({ProgressRatio:P1})";
            }
        }

        // ✅ 체크 상태를 DB에 저장하는 메소드
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