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
                SetProperty(ref _progress, value);
                OnPropertyChanged(nameof(ProgressWidth));
                OnPropertyChanged(nameof(Tooltip));
            }
        }

        // 학습 시간 (분 단위로 저장)
        private int _studyTimeMinutes;
        public int StudyTimeMinutes
        {
            get => _studyTimeMinutes;
            set
            {
                SetProperty(ref _studyTimeMinutes, value);
                OnPropertyChanged(nameof(Tooltip));
                OnPropertyChanged(nameof(StudyTimeText));
            }
        }

        // Tooltip에 표시될 시간 텍스트
        public string Tooltip
        {
            get
            {
                var hours = StudyTimeMinutes / 60;
                var minutes = StudyTimeMinutes % 60;
                return $"{hours:D2}:{minutes:D2}:{0:D2}"; // HH:MM:SS 형식
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

        // Progress Bar 너비 계산 (동적 계산)
        private double _maxWidth = 200; // 기본값
        public double MaxWidth
        {
            get => _maxWidth;
            set
            {
                _maxWidth = value;
                OnPropertyChanged(nameof(ProgressWidth));
            }
        }

        public double ProgressWidth => MaxWidth * Math.Max(0, Math.Min(1, Progress));

        // TopicGroup 리스트 (드래그 앤 드롭으로 추가된 분류들)
        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        public SubjectProgressViewModel()
        {
            // 초기값 설정
            Progress = 0.0;
            StudyTimeMinutes = 0;
        }

        // 과목 진행률 업데이트 메소드
        public void UpdateProgress(double newProgress)
        {
            Progress = Math.Max(0, Math.Min(1, newProgress)); // 0-1 사이로 제한
        }

        // 학습 시간 추가 메소드
        public void AddStudyTime(int minutes)
        {
            StudyTimeMinutes += Math.Max(0, minutes);
        }

        // TopicGroup 추가 메소드
        public void AddTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && !TopicGroups.Contains(topicGroup))
            {
                TopicGroups.Add(topicGroup);
            }
        }

        // TopicGroup 제거 메소드
        public void RemoveTopicGroup(TopicGroupViewModel topicGroup)
        {
            if (topicGroup != null && TopicGroups.Contains(topicGroup))
            {
                TopicGroups.Remove(topicGroup);
            }
        }

        // 과목 정보 요약
        public override string ToString()
        {
            return $"{SubjectName} - {Progress:P1} ({StudyTimeText})";
        }
    }
}