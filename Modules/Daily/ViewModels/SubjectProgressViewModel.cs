using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;
using SP.ViewModels;
using System;

namespace SP.Modules.Daily.ViewModels
{
    public class SubjectProgressViewModel : ViewModelBase
    {
        private string _subjectName = string.Empty;
        public string SubjectName
        {
            get => _subjectName;
            set => SetProperty(ref _subjectName, value);
        }

        // ✅ 수정: Progress는 사용자가 직접 설정하는 값 (기존 유지)
        private double _progress = 0.0; // 0.0 ~ 1.0 사이의 값
        public double Progress
        {
            get => _progress;
            set
            {
                var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
                if (SetProperty(ref _progress, clampedValue))
                {
                    // ✅ 추가: Progress 변경 시 다른 프로퍼티들도 업데이트
                    OnPropertyChanged(nameof(ProgressWidth));
                    OnPropertyChanged(nameof(ProgressPercentText));
                    OnPropertyChanged(nameof(Tooltip));
                }
            }
        }

        // ✅ 추가: 총 공부시간 설정을 위한 정적 변수
        private static int _totalDailyStudyTimeSeconds = 0;

        // ✅ 추가: 총 공부시간 설정 메소드 (외부에서 호출)
        public static void SetTotalDailyStudyTime(int totalSeconds)
        {
            _totalDailyStudyTimeSeconds = totalSeconds;
            System.Diagnostics.Debug.WriteLine($"[SubjectProgress] 오늘 총 공부시간 설정: {totalSeconds}초");
        }

        // ✅ 추가: 실제 진행률 계산 (총 공부시간 대비)
        public double ActualProgress
        {
            get
            {
                if (_totalDailyStudyTimeSeconds == 0)
                    return 0.0;

                var ratio = (double)TodayStudyTimeSeconds / _totalDailyStudyTimeSeconds;
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} 실제 진행률: {ratio:P2} ({TodayStudyTimeSeconds}/{_totalDailyStudyTimeSeconds})");
                return Math.Min(1.0, ratio);
            }
        }

        // ✅ 수정: 오늘의 학습 시간 (초 단위)
        private int _todayStudyTimeSeconds;
        public int TodayStudyTimeSeconds
        {
            get => _todayStudyTimeSeconds;
            set
            {
                if (SetProperty(ref _todayStudyTimeSeconds, value))
                {
                    OnPropertyChanged(nameof(StudyTimeText));
                    OnPropertyChanged(nameof(ActualProgress)); // ✅ 추가: 실제 진행률 업데이트
                    OnPropertyChanged(nameof(Tooltip));

                    // ✅ TopicGroups에게 오늘의 부모 시간 알려주기
                    UpdateTopicGroupsParentTime();
                }
            }
        }

        // ✅ 수정: 호환성을 위한 프로퍼티 (기존 코드들이 분 단위로 접근)
        public int StudyTimeMinutes
        {
            get => TodayStudyTimeSeconds / 60;
            set => TodayStudyTimeSeconds = value * 60;
        }

        // ✅ 삭제: 중복된 _studyTimeMinutes 필드 제거
        // private int _studyTimeMinutes; // ❌ 삭제

        // ✅ TopicGroups에게 부모의 오늘 학습시간 전달
        private void UpdateTopicGroupsParentTime()
        {
            foreach (var topicGroup in TopicGroups)
            {
                topicGroup.SetParentTodayStudyTime(TodayStudyTimeSeconds);
            }
        }

        public string StudyTimeText
        {
            get
            {
                var hours = TodayStudyTimeSeconds / 3600;
                var minutes = (TodayStudyTimeSeconds % 3600) / 60;
                var seconds = TodayStudyTimeSeconds % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
        }

        // Progress Bar 너비 계산 (동적 계산)
        private double _maxWidth = 200; // 기본값
        public double MaxWidth
        {
            get => _maxWidth;
            set
            {
                if (SetProperty(ref _maxWidth, value))
                {
                    OnPropertyChanged(nameof(ProgressWidth));
                    OnPropertyChanged(nameof(ActualProgressWidth)); // ✅ 추가
                }
            }
        }

        // ✅ 수정: 기존 Progress 기반 너비 (사용자 설정값)
        public double ProgressWidth => MaxWidth * Progress;

        // ✅ 추가: 실제 진행률 기반 너비 (총 공부시간 대비)
        public double ActualProgressWidth => MaxWidth * ActualProgress;

        // 진행률 퍼센트 텍스트 (진행률 바 위에 표시용)
        public string ProgressPercentText => $"{Progress:P0}";

        // ✅ 추가: 실제 진행률 퍼센트 텍스트
        public string ActualProgressPercentText => $"{ActualProgress:P0}";

        // ✅ 수정: Tooltip에 실제 진행률 표시
        public string Tooltip => $"{SubjectName}: {ActualProgress:P1} - {StudyTimeText}";

        // TopicGroup 리스트 (드래그 앤 드롭으로 추가된 분류들)
        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        // 🆕 무한 루프 방지를 위한 플래그들
        public bool _isUpdatingFromDatabase = false;
        public bool _isSavingToDatabase = false;

        public SubjectProgressViewModel()
        {
            // 초기값 설정
            Progress = 0.0;
            TodayStudyTimeSeconds = 0; // ✅ 수정: StudyTimeMinutes 대신 TodayStudyTimeSeconds 사용

            // TopicGroups 변경 감지 - 개선된 로직
            TopicGroups.CollectionChanged += (s, e) =>
            {
                // 🆕 무한 루프 방지 - DB 업데이트 중이거나 저장 중이면 무시
                if (_isUpdatingFromDatabase || _isSavingToDatabase)
                {
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} TopicGroups 변경 무시됨 (플래그 상태: 업데이트={_isUpdatingFromDatabase}, 저장={_isSavingToDatabase})");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}의 TopicGroups 변경됨. 현재 개수: {TopicGroups.Count}");

                // 🆕 TopicGroups 변경 시 DB에 저장
                SaveToDatabase();
            };
        }

        // ✅ 수정: DB에서 데이터를 업데이트할 때 사용하는 메소드 (무한루프 방지)
        public void UpdateFromDatabase(double progress, int studyTimeMinutes, ObservableCollection<TopicGroupViewModel> topicGroups)
        {
            _isUpdatingFromDatabase = true;
            try
            {
                // 기본 속성 업데이트
                Progress = progress;
                TodayStudyTimeSeconds = studyTimeMinutes * 60; // ✅ 수정: 분을 초로 변환

                // TopicGroups 업데이트
                TopicGroups.Clear();
                foreach (var group in topicGroups)
                {
                    TopicGroups.Add(group);
                }

                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} DB에서 업데이트됨: {TopicGroups.Count}개 그룹, 오늘시간: {TodayStudyTimeSeconds}초");
            }
            finally
            {
                _isUpdatingFromDatabase = false;
            }
        }

        // 🆕 DB에 저장하는 메소드 수정 - 무한루프 방지
        private void SaveToDatabase()
        {
            if (_isSavingToDatabase || _isUpdatingFromDatabase)
            {
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} 저장 스킵됨 (이미 저장 중이거나 업데이트 중)");
                return;
            }

            if (string.IsNullOrEmpty(SubjectName))
            {
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] SubjectName이 비어있어 저장 스킵됨");
                return;
            }

            _isSavingToDatabase = true;
            try
            {
                var dbHelper = SP.Modules.Common.Helpers.DatabaseHelper.Instance;

                // 🆕 TopicGroups도 함께 저장하는 새로운 메소드 사용
                dbHelper.SaveDailySubjectWithTopicGroups(DateTime.Today, SubjectName, Progress, StudyTimeMinutes, 0, TopicGroups);

                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}과 TopicGroups({TopicGroups.Count}개) DB에 저장됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] DB 저장 오류: {ex.Message}");
            }
            finally
            {
                _isSavingToDatabase = false;
            }
        }

        // 과목 진행률 업데이트 메소드
        public void UpdateProgress(double newProgress)
        {
            Progress = newProgress;
        }

        // ✅ 수정: 학습 시간 추가 메소드 (초 단위로 변경)
        public void AddStudyTime(int seconds)
        {
            TodayStudyTimeSeconds += Math.Max(0, seconds);

            // ✅ 수정: 자동 진행률 계산 로직 제거 (사용자가 직접 설정)
            // Progress는 사용자가 직접 설정하는 값으로 유지
            System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} 학습시간 추가: {seconds}초, 총: {TodayStudyTimeSeconds}초");
        }

        // TopicGroup 추가 메소드 - 개선됨
        public void AddTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && !TopicGroups.Contains(topicGroup))
            {
                topicGroup.ParentSubjectName = SubjectName; // 부모 정보 설정

                // ✅ 추가: 부모의 오늘 학습시간 설정
                topicGroup.SetParentTodayStudyTime(TodayStudyTimeSeconds);

                // 🆕 직접 추가하지 않고 안전한 방법 사용
                _isUpdatingFromDatabase = true;
                try
                {
                    TopicGroups.Add(topicGroup);
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}에 TopicGroup '{topicGroup.GroupTitle}' 추가됨 (부모 오늘시간: {TodayStudyTimeSeconds}초)");
                }
                finally
                {
                    _isUpdatingFromDatabase = false;
                }
            }
        }

        // TopicGroup 제거 메소드 - 개선됨
        public void RemoveTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && TopicGroups.Contains(topicGroup))
            {
                // 🆕 직접 제거하지 않고 안전한 방법 사용
                _isUpdatingFromDatabase = true;
                try
                {
                    TopicGroups.Remove(topicGroup);
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}에서 TopicGroup '{topicGroup.GroupTitle}' 제거됨");
                }
                finally
                {
                    _isUpdatingFromDatabase = false;
                }
            }
        }

        // ✅ 수정: 과목 정보 요약에 실제 진행률 추가
        public override string ToString()
        {
            return $"{SubjectName} - Progress: {Progress:P1}, Actual: {ActualProgress:P1} ({StudyTimeText}) [TopicGroups: {TopicGroups.Count}]";
        }
    }
}