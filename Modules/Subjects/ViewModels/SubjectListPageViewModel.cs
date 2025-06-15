using SP.Modules.Common.Helpers;
using SP.Modules.Common.ViewModels;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;


namespace SP.Modules.Subjects.ViewModels
{
    public class SubjectListPageViewModel : ViewModelBase
    {
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
                    var helper = new DatabaseHelper();

                    // DB에 저장
                    int subjectId = helper.AddSubject(NewSubjectText);

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
            var helper = new DatabaseHelper();
            var subjectList = helper.LoadSubjectsWithGroups(); // 이 메서드 구현 필요

            foreach (var subject in subjectList)
            {
                Subjects.Add(subject);
            }
        }

    }

}