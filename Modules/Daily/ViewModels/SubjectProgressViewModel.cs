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

        // ✅ 완전 자동화: 실시간 측정 기반 진행률만 사용
        public double Progress => CalculateActualProgress();

        // ✅ 총 공부시간 설정을 위한 정적 변수 (초단위)
        private static int _totalDailyStudyTimeSeconds = 0;

        public static void SetTotalDailyStudyTime(int totalSeconds)
        {
            _totalDailyStudyTimeSeconds = totalSeconds;
            System.Diagnostics.Debug.WriteLine($"[SubjectProgress] 오늘 총 공부시간 설정: {totalSeconds}초");
        }

        // ✅ 실시간 진행률 계산 (총 공부시간 대비)
        private double CalculateActualProgress()
        {
            if (_totalDailyStudyTimeSeconds == 0)
                return 0.0;

            var ratio = (double)TodayStudyTimeSeconds / _totalDailyStudyTimeSeconds;
            return Math.Min(1.0, ratio);
        }

        // ✅ 오늘의 학습 시간 (초 단위) - 메인 프로퍼티
        private int _todayStudyTimeSeconds;
        public int TodayStudyTimeSeconds
        {
            get => _todayStudyTimeSeconds;
            set
            {
                if (SetProperty(ref _todayStudyTimeSeconds, value))
                {
                    OnPropertyChanged(nameof(StudyTimeText));
                    OnPropertyChanged(nameof(StudyTimeMinutes)); // 호환성
                    OnPropertyChanged(nameof(Progress)); // ✅ 자동 계산된 진행률
                    OnPropertyChanged(nameof(ProgressWidth));
                    OnPropertyChanged(nameof(ProgressPercentText));
                    OnPropertyChanged(nameof(Tooltip));

                    // TopicGroups에게 오늘의 부모 시간 알려주기
                    UpdateTopicGroupsParentTime();
                }
            }
        }

        // ✅ 호환성을 위한 프로퍼티 (기존 코드들이 분 단위로 접근)
        public int StudyTimeMinutes
        {
            get => TodayStudyTimeSeconds / 60;
            set => TodayStudyTimeSeconds = value * 60;
        }

        // ✅ TopicGroups에게 부모의 오늘 학습시간 전달
        private void UpdateTopicGroupsParentTime()
        {
            foreach (var topicGroup in TopicGroups)
            {
                topicGroup.SetParentTodayStudyTime(TodayStudyTimeSeconds);
            }
        }

        // ✅ 시간 표시 텍스트 (00:00:00 형식)
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
        private double _maxWidth = 200;
        public double MaxWidth
        {
            get => _maxWidth;
            set
            {
                if (SetProperty(ref _maxWidth, value))
                {
                    OnPropertyChanged(nameof(ProgressWidth));
                }
            }
        }

        // ✅ 자동 계산된 Progress 기반 너비
        public double ProgressWidth => MaxWidth * Progress;

        // ✅ 자동 계산된 진행률 퍼센트 텍스트
        public string ProgressPercentText => $"{Progress:P0}";

        // ✅ Tooltip에 실제 진행률 표시
        public string Tooltip => $"{SubjectName}: {Progress:P1} - {StudyTimeText}";

        // TopicGroup 리스트 (드래그 앤 드롭으로 추가된 분류들)
        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        // 무한 루프 방지를 위한 플래그들
        public bool _isUpdatingFromDatabase = false;
        public bool _isSavingToDatabase = false;

        public SubjectProgressViewModel()
        {
            // 초기값 설정
            TodayStudyTimeSeconds = 0;

            // TopicGroups 변경 감지
            TopicGroups.CollectionChanged += (s, e) =>
            {
                if (_isUpdatingFromDatabase || _isSavingToDatabase)
                {
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} TopicGroups 변경 무시됨");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}의 TopicGroups 변경됨. 현재 개수: {TopicGroups.Count}");
                SaveToDatabase();
            };
        }

        // ✅ DB에서 데이터를 업데이트할 때 사용하는 메소드 (진행률 제거)
        public void UpdateFromDatabase(int studyTimeSeconds, ObservableCollection<TopicGroupViewModel> topicGroups)
        {
            _isUpdatingFromDatabase = true;
            try
            {
                // 시간만 업데이트 (진행률은 자동 계산)
                TodayStudyTimeSeconds = studyTimeSeconds;

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

        // ✅ DB에 저장하는 메소드 수정 - 진행률 자동 계산해서 저장
        private void SaveToDatabase()
        {
            if (_isSavingToDatabase || _isUpdatingFromDatabase)
            {
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} 저장 스킵됨");
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

                // ✅ 자동 계산된 진행률로 저장
                dbHelper.SaveDailySubjectWithTopicGroups(DateTime.Today, SubjectName, Progress, TodayStudyTimeSeconds, 0, TopicGroups);

                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}과 TopicGroups({TopicGroups.Count}개) DB에 저장됨 (진행률: {Progress:P1}, {TodayStudyTimeSeconds}초)");
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

        // ✅ 학습 시간 추가 메소드 (초 단위) - 실시간 측정으로 호출됨
        public void AddStudyTime(int seconds)
        {
            TodayStudyTimeSeconds += Math.Max(0, seconds);
            System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} 학습시간 추가: {seconds}초, 총: {TodayStudyTimeSeconds}초, 진행률: {Progress:P1}");
        }

        // TopicGroup 추가 메소드
        public void AddTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && !TopicGroups.Contains(topicGroup))
            {
                topicGroup.ParentSubjectName = SubjectName;
                topicGroup.SetParentTodayStudyTime(TodayStudyTimeSeconds);

                _isUpdatingFromDatabase = true;
                try
                {
                    TopicGroups.Add(topicGroup);
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}에 TopicGroup '{topicGroup.GroupTitle}' 추가됨");
                }
                finally
                {
                    _isUpdatingFromDatabase = false;
                }
            }
        }

        // TopicGroup 제거 메소드
        public void RemoveTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && TopicGroups.Contains(topicGroup))
            {
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

        public override string ToString()
        {
            return $"{SubjectName} - Progress: {Progress:P1} ({StudyTimeText}) [TopicGroups: {TopicGroups.Count}]";
        }
    }
}