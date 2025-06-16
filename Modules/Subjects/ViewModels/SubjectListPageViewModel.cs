using SP.Modules.Common.Helpers;
using SP.Modules.Common.ViewModels;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SP.Modules.Subjects.ViewModels
{
    public class SubjectListPageViewModel : ViewModelBase
    {
<<<<<<< HEAD
<<<<<<< Updated upstream
=======
        // 싱글톤 인스턴스 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;

>>>>>>> Stashed changes
=======
        // 싱글톤 DB 헬퍼 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;

>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244
        public ObservableCollection<SubjectGroupViewModel> Subjects { get; set; } = new();

        private bool _isAdding;
        public bool IsAdding
        {
            get => _isAdding;
            set
            {
                _isAdding = value;
                OnPropertyChanged(nameof(IsAdding));
            }
        }

        public ICommand StartAddCommand { get; }
        public ICommand AddSubjectCommand { get; }

        private string _newSubjectText;
        public string NewSubjectText
        {
            get => _newSubjectText;
            set => SetProperty(ref _newSubjectText, value);
        }

        public SubjectListPageViewModel()
        {
            Subjects = new ObservableCollection<SubjectGroupViewModel>();
            LoadSubjects();

            StartAddCommand = new RelayCommand(() => IsAdding = true);

            AddSubjectCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(NewSubjectText))
                {
                    // DB에 저장
                    int subjectId = _db.AddSubject(NewSubjectText);

                    // ViewModel에 추가
                    Subjects.Add(new SubjectGroupViewModel
                    {
                        SubjectId = subjectId,
                        SubjectName = NewSubjectText,
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>(),
                        TotalStudyTime = 0
                    });

                    NewSubjectText = string.Empty;
                    IsAdding = false;

                    // 전역 총 시간 업데이트
                    UpdateGlobalTotalTime();
                }
            });
        }
<<<<<<< HEAD
<<<<<<< Updated upstream
        private void LoadSubjects()
        {
            var helper = new DatabaseHelper();
            var subjectList = helper.LoadSubjectsWithGroups(); // 이 메서드 구현 필요
=======
=======

        private void LoadSubjects()
        {
            var subjectList = _db.LoadSubjectsWithGroups();
>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244

        // ✅ 실제 DB에서 과목 로드 (더미 데이터 제거)
        private void LoadSubjects()
        {
            Subjects.Clear();
>>>>>>> Stashed changes

            // 전체 학습시간 계산 및 설정
            int totalAllSubjectsTime = _db.GetTotalAllSubjectsStudyTime();
            SubjectGroupViewModel.SetGlobalTotalTime(totalAllSubjectsTime);

            var subjectList = _db.LoadSubjectsWithGroups();
            foreach (var subject in subjectList)
            {
                Subjects.Add(subject);
            }
        }
<<<<<<< HEAD

<<<<<<< Updated upstream
=======
        private void UpdateGlobalTotalTime()
        {
            int totalAllSubjectsTime = _db.GetTotalAllSubjectsStudyTime();
            SubjectGroupViewModel.SetGlobalTotalTime(totalAllSubjectsTime);

            // 모든 과목의 진행률 업데이트 (접근 가능한 메서드 사용)
            foreach (var subject in Subjects)
            {
                // OnPropertyChanged는 protected이므로 NotifyPropertyChanged 호출
                subject.NotifyProgressChanged();
            }
        }

        // 외부에서 데이터 새로고침할 때 사용
        public void RefreshData()
        {
            LoadSubjects();
        }
>>>>>>> Stashed changes
=======
>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244
    }
}