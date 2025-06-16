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

        private double _progress;
        public double Progress
        {
            get => _progress;
            set
            {
                // 진행률은 0.0 ~ 1.0 사이 값으로 제한
                var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
                if (SetProperty(ref _progress, clampedValue))
                {
                    OnPropertyChanged(nameof(ProgressWidth));
                    OnPropertyChanged(nameof(Tooltip));
                    OnPropertyChanged(nameof(ProgressPercentText));
                    System.Diagnostics.Debug.WriteLine($"[Progress] {SubjectName} 진행률 업데이트: {_progress:P1}");
                }
            }
        }

        // 학습 시간 (분 단위로 저장)
        private int _studyTimeMinutes;
        public int StudyTimeMinutes
        {
            get => _studyTimeMinutes;
            set
            {
                if (SetProperty(ref _studyTimeMinutes, value))
                {
                    OnPropertyChanged(nameof(Tooltip));
                    OnPropertyChanged(nameof(StudyTimeText));
                }
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
                }
            }
        }

        // 진행률 바 너비 계산
        public double ProgressWidth => MaxWidth * Progress;

        // 진행률 퍼센트 텍스트 (진행률 바 위에 표시용)
        public string ProgressPercentText => $"{Progress:P0}";

        // Tooltip에 표시될 시간 텍스트
        public string Tooltip
        {
            get
            {
                var hours = StudyTimeMinutes / 60;
                var minutes = StudyTimeMinutes % 60;
                return $"{SubjectName}: {Progress:P1} - {hours:D2}:{minutes:D2}:{0:D2}";
            }
        }

        // 학습 시간을 텍스트로 표시
        public string StudyTimeText
        {
            get
            {
                var hours = StudyTimeMinutes / 60;
                var minutes = StudyTimeMinutes % 60;
                return $"{hours}시간 {minutes}분";
            }
        }

        // TopicGroup 리스트 (드래그 앤 드롭으로 추가된 분류들)
        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        public SubjectProgressViewModel()
        {
            // 초기값 설정
            Progress = 0.0;
            StudyTimeMinutes = 0;

            // TopicGroups 변경 감지
            TopicGroups.CollectionChanged += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}의 TopicGroups 변경됨. 현재 개수: {TopicGroups.Count}");

                // 🆕 TopicGroups 변경 시 DB에 저장
                SaveToDatabase();
            };
        }

        // 🆕 DB에 저장하는 메소드 추가
        private void SaveToDatabase()
        {
            if (!string.IsNullOrEmpty(SubjectName))
            {
                try
                {
                    var dbHelper = SP.Modules.Common.Helpers.DatabaseHelper.Instance;
                    dbHelper.SaveDailySubject(DateTime.Today, SubjectName, Progress, StudyTimeMinutes, 0);
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} DB에 저장됨");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgress] DB 저장 오류: {ex.Message}");
                }
            }
        }

        // 과목 진행률 업데이트 메소드
        public void UpdateProgress(double newProgress)
        {
            Progress = newProgress;
        }

        // 학습 시간 추가 메소드
        public void AddStudyTime(int minutes)
        {
            StudyTimeMinutes += Math.Max(0, minutes);

            // 학습 시간에 따른 자동 진행률 계산 (예: 120분 = 100%)
            var calculatedProgress = Math.Min(1.0, StudyTimeMinutes / 120.0);
            if (calculatedProgress > Progress)
            {
                Progress = calculatedProgress;
            }
        }

        // TopicGroup 추가 메소드
        public void AddTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && !TopicGroups.Contains(topicGroup))
            {
                topicGroup.ParentSubjectName = SubjectName; // 부모 정보 설정
                TopicGroups.Add(topicGroup);
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}에 TopicGroup '{topicGroup.GroupTitle}' 추가됨");
            }
        }

        // TopicGroup 제거 메소드
        public void RemoveTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && TopicGroups.Contains(topicGroup))
            {
                TopicGroups.Remove(topicGroup);
                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName}에서 TopicGroup '{topicGroup.GroupTitle}' 제거됨");
            }
        }

        // 과목 정보 요약
        public override string ToString()
        {
            return $"{SubjectName} - {Progress:P1} ({StudyTimeText}) [TopicGroups: {TopicGroups.Count}]";
        }
    }
}