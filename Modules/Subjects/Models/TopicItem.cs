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

        // 🆕 Content 속성 추가 (Name과 동일한 역할)
        public string Content
        {
            get => _name;
            set => Name = value;
        }

        private double _progress = 0.0;
        public double Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = Math.Max(0.0, Math.Min(1.0, value)); // 0-1 사이로 제한
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

                    // 학습 시간에 따라 Progress 자동 계산 (예: 120분 = 100%)
                    if (_studyTimeMinutes > 0)
                    {
                        Progress = Math.Min(1.0, _studyTimeMinutes / 120.0);
                    }
                }
            }
        }

        // 🆕 체크 상태 추가 - DB 저장 기능 포함
        private bool _isCompleted = false;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    OnPropertyChanged(nameof(IsCompleted));

                    // 🆕 체크 상태 변경 시 DB에 저장
                    SaveCheckStateToDatabase();
                }
            }
        }

        // 드래그 앤 드롭을 위한 부모 정보
        public string ParentTopicGroupName { get; set; } = string.Empty;
        public string ParentSubjectName { get; set; } = string.Empty;

        // 🆕 Progress Bar Tooltip - 00:00:00 형식으로 수정
        public string ProgressTooltip
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
            // 초기값 설정
            Progress = 0.0;
            StudyTimeMinutes = 0;
            IsCompleted = false;
        }

        // 🆕 체크 상태를 DB에 저장하는 메소드
        private void SaveCheckStateToDatabase()
        {
            try
            {
                if (!string.IsNullOrEmpty(ParentSubjectName) && !string.IsNullOrEmpty(ParentTopicGroupName) && !string.IsNullOrEmpty(Name))
                {
                    var dbHelper = SP.Modules.Common.Helpers.DatabaseHelper.Instance;
                    dbHelper.UpdateDailyTopicItemCompletion(DateTime.Today, ParentSubjectName, ParentTopicGroupName, Name, IsCompleted);
                    System.Diagnostics.Debug.WriteLine($"[TopicItem] 체크 상태 저장: {Name} = {IsCompleted}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TopicItem] 체크 상태 저장 오류: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}