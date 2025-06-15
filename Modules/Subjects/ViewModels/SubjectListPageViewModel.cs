using SP.Modules.Common.Helpers;
using SP.Modules.Common.ViewModels;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SP.Modules.Subjects.ViewModels
{
    public class SubjectListPageViewModel : ViewModelBase
    {
        // 싱글톤 DB 헬퍼 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;

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
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                    });

                    NewSubjectText = string.Empty;
                    IsAdding = false;
                }
            });
        }

        private void LoadSubjects()
        {
            var subjectList = _db.LoadSubjectsWithGroups();

            foreach (var subject in subjectList)
            {
                Subjects.Add(subject);
            }
        }
    }
}