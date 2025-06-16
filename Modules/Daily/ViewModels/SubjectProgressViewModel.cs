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
                    OnPropertyChanged(nameof(StudyTimeText));
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

        // 🆕 학습 시간을 00:00:00 형식으로 표시 (과목용)
        public string StudyTimeText
        {
            get
            {
                var totalSeconds = StudyTimeMinutes * 60;
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
        }

        // TopicGroup 리스트 (드래그 앤 드롭으로 추가된 분류들)
        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        // 🆕 무한 루프 방지를 위한 플래그들
        private bool _isUpdatingFromDatabase = false;
        private bool _isSavingToDatabase = false;

        public SubjectProgressViewModel()
        {
            // 초기값 설정
            Progress = 0.0;
            StudyTimeMinutes = 0;

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

        // 🆕 DB에서 데이터를 업데이트할 때 사용하는 메소드 (무한루프 방지)
        public void UpdateFromDatabase(double progress, int studyTimeMinutes, ObservableCollection<TopicGroupViewModel> topicGroups)
        {
            _isUpdatingFromDatabase = true;
            try
            {
                // 기본 속성 업데이트
                Progress = progress;
                StudyTimeMinutes = studyTimeMinutes;

                // TopicGroups 업데이트
                TopicGroups.Clear();
                foreach (var group in topicGroups)
                {
                    TopicGroups.Add(group);
                }

                System.Diagnostics.Debug.WriteLine($"[SubjectProgress] {SubjectName} DB에서 업데이트됨: {TopicGroups.Count}개 그룹");
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

        // TopicGroup 추가 메소드 - 개선됨
        public void AddTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && !TopicGroups.Contains(topicGroup))
            {
                topicGroup.ParentSubjectName = SubjectName; // 부모 정보 설정

                // 🆕 직접 추가하지 않고 안전한 방법 사용
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

        // 과목 정보 요약
        public override string ToString()
        {
            return $"{SubjectName} - {Progress:P1} ({StudyTimeText}) [TopicGroups: {TopicGroups.Count}]";
        }
    }
}