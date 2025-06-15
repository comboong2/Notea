using System;
using System.ComponentModel;

namespace SP.Modules.Subjects.Models
{
    public class TopicItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int SubjectId { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressTooltip));
                    OnPropertyChanged(nameof(StudyTimeText));
                }
            }
        }

        // 학습 시간 (분 단위)
        private int _studyTimeMinutes;
        public int StudyTimeMinutes
        {
            get => _studyTimeMinutes;
            set
            {
                if (_studyTimeMinutes != value)
                {
                    _studyTimeMinutes = value;
                    OnPropertyChanged(nameof(StudyTimeMinutes));
                    OnPropertyChanged(nameof(ProgressTooltip));
                    OnPropertyChanged(nameof(StudyTimeText));
                }
            }
        }

        // Progress Bar Tooltip
        public string ProgressTooltip
        {
            get
            {
                var hours = StudyTimeMinutes / 60;
                var minutes = StudyTimeMinutes % 60;
                return $"{hours:D2}:{minutes:D2}:{0:D2}"; // HH:MM:SS 형식
            }
        }

        // 학습 시간 텍스트
        public string StudyTimeText
        {
            get
            {
                var hours = StudyTimeMinutes / 60;
                var minutes = StudyTimeMinutes % 60;
                return $"{hours}시간 {minutes}분";
            }
        }

        public TopicItem()
        {
            // Progress에 따라 임시 학습 시간 설정 (실제로는 DB에서 가져와야 함)
            StudyTimeMinutes = (int)(Progress * 120);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}