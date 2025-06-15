using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;
using SP.ViewModels;
using System;

namespace SP.Modules.Daily.ViewModels
{
    public class SubjectProgressViewModel : ViewModelBase
    {
        public string SubjectName { get; set; }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
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

        public double ProgressWidth => 200 * Progress; // 바 너비 계산용

        public ObservableCollection<TopicGroupViewModel> TopicGroups { get; set; } = new();

        public SubjectProgressViewModel()
        {
            // 테스트용 데이터 - 실제로는 DB에서 가져와야 함
            StudyTimeMinutes = (int)(Progress * 300); // Progress에 따라 임시 시간 설정
        }
    }
}