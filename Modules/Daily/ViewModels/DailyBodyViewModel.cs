using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Daily.Models;
using SP.ViewModels;

namespace SP.Modules.Daily.ViewModels
{
    public class DailyBodyViewModel : ViewModelBase
    {
        // 과목 리스트
        public ObservableCollection<SubjectProgressViewModel> Subjects { get; set; }

        // TODO 리스트
        public ObservableCollection<TodoItem> TodoList { get; set; }

        // 싱글톤 DB 헬퍼 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;

        // 새 할 일 텍스트
        private string _newTodoText;
        public string NewTodoText
        {
            get => _newTodoText;
            set => SetProperty(ref _newTodoText, value);
        }

        // 입력 모드 여부
        private bool _isAdding = false;
        public bool IsAdding
        {
            get => _isAdding;
            set => SetProperty(ref _isAdding, value);
        }

        // 포커스 요청용 이벤트 (View에서 연결)
        public Action? RequestFocusOnInput { get; set; }

        public ICommand AddTodoCommand { get; }
        public ICommand StartAddCommand { get; }
        public ICommand DeleteTodoCommand { get; }

        public DailyBodyViewModel(DateTime appStartDate)
        {
            SelectedDate = appStartDate;

            // 빈 컬렉션으로 시작
            Subjects = new ObservableCollection<SubjectProgressViewModel>();

            TodoList = new ObservableCollection<TodoItem>();

            AddTodoCommand = new RelayCommand(AddTodo);
            StartAddCommand = new RelayCommand(() =>
            {
                IsAdding = true;
                RequestFocusOnInput?.Invoke();
            });
            DeleteTodoCommand = new RelayCommand<TodoItem>(DeleteTodo);

            // comment, TodoList, DailySubjects 불러오기
            LoadDailyData(SelectedDate);

            // CollectionChanged 이벤트를 맨 마지막에 연결
            Subjects.CollectionChanged += Subjects_CollectionChanged;
        }

        private void Subjects_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 무한 루프 방지를 위한 플래그 체크
            if (_isLoadingSubjects) return;

            SaveDailySubjects();
        }

        private bool _isLoadingSubjects = false;

        private void AddTodo()
        {
            if (!string.IsNullOrWhiteSpace(NewTodoText))
            {
                string trimmed = NewTodoText.Trim();
                int id = _db.AddTodo(SelectedDate, trimmed); // DB에 저장 + ID 받기

                var newItem = new TodoItem
                {
                    Id = id,
                    Title = trimmed,
                    IsCompleted = false
                };

                // PropertyChanged 이벤트 구독
                newItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TodoItem.IsCompleted))
                    {
                        _db.UpdateTodoCompletion(newItem.Id, newItem.IsCompleted);
                    }
                };

                TodoList.Add(newItem);
                NewTodoText = string.Empty;
            }
            IsAdding = false;
        }

        private void DeleteTodo(TodoItem todo)
        {
            if (todo == null)
            {
                System.Diagnostics.Debug.WriteLine("[Todo] 삭제할 Todo가 null입니다.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Todo] Todo 삭제 시도: {todo.Title} (ID: {todo.Id})");

            try
            {
                _db.DeleteTodo(todo.Id);
                TodoList.Remove(todo);
                System.Diagnostics.Debug.WriteLine($"[Todo] Todo 삭제 완료: {todo.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Todo] Todo 삭제 오류: {ex.Message}");
            }
        }

        // DailyBodyView.xaml.cs에서 호출할 수 있도록 public으로 변경
        public void DeleteTodoItem(TodoItem todo)
        {
            DeleteTodo(todo);
        }

        // 헤더 하단의 comment, d-day 관련
        private string _comment = string.Empty;
        public string Comment
        {
            get => _comment;
            set
            {
                if (SetProperty(ref _comment, value))
                {
                    _db.SaveOrUpdateComment(SelectedDate, _comment); // 저장
                }
            }
        }

        public void LoadDailyData(DateTime date)
        {
            SelectedDate = date;

            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] LoadDailyData 호출됨. 날짜: {date.ToShortDateString()}");

            // Comment 불러오기
            Comment = _db.GetCommentByDate(date);
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] Comment 로드됨: '{Comment}'");

            // TodoList 불러오기
            var todos = _db.GetTodosByDate(date);
            TodoList.Clear();
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 투두 항목 {todos.Count}개 DB에서 로드됨.");

            foreach (var todo in todos)
            {
                //  IsCompleted 변경될 때마다 DB 반영
                todo.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TodoItem.IsCompleted))
                    {
                        _db.UpdateTodoCompletion(todo.Id, todo.IsCompleted);
                    }
                };

                TodoList.Add(todo);
            }
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] TodoList에 {TodoList.Count}개 항목 추가됨.");

            // 오늘 할 일 과목 리스트 불러오기
            LoadDailySubjects(date);
        }

        private void LoadDailySubjects(DateTime date)
        {
            _isLoadingSubjects = true; // 무한 루프 방지 플래그 설정

            var dailySubjects = _db.GetDailySubjects(date);

            Subjects.Clear();
            foreach (var (subjectName, progress, studyTimeMinutes) in dailySubjects)
            {
                Subjects.Add(new SubjectProgressViewModel
                {
                    SubjectName = subjectName,
                    Progress = progress,
                    StudyTimeMinutes = studyTimeMinutes
                });
            }

            _isLoadingSubjects = false; // 플래그 해제

            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 {Subjects.Count}개 로드됨");
        }

        private void SaveDailySubjects()
        {
            try
            {
                // 기존 데이터 삭제 후 다시 저장 (간단한 방법)
                for (int i = 0; i < Subjects.Count; i++)
                {
                    var subject = Subjects[i];
                    _db.SaveDailySubject(SelectedDate, subject.SubjectName, subject.Progress, subject.StudyTimeMinutes, i);
                }
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 저장 완료: {Subjects.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 저장 오류: {ex.Message}");
            }
        }

        public void AddSubjectSafely(SubjectProgressViewModel subject)
        {
            if (subject == null) return;

            // 중복 확인
            var existingSubject = Subjects.FirstOrDefault(s =>
                string.Equals(s.SubjectName, subject.SubjectName, StringComparison.OrdinalIgnoreCase));

            if (existingSubject == null)
            {
                Subjects.Add(subject);
            }
        }



        public string InfoTitle => IsToday ? "시험" : "총 학습 시간";
        public string InfoContent => IsToday ? $"D-{Dday}" : TotalStudyTime;

        public bool IsToday => SelectedDate.Date == DateTime.Today;

        private DateTime _selectedDate;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    // 날짜가 바뀌면 Comment를 다시 불러오고, 할 일도 갱신하도록 처리
                    Comment = _db.GetCommentByDate(value);
                }
            }
        }

        public TimeSpan StudyTime { get; set; } = TimeSpan.FromMinutes(165);
        public int Dday => (TargetDate - DateTime.Today).Days;
        public DateTime TargetDate { get; set; } = new DateTime(2025, 6, 22);

        public string TotalStudyTime => $"{(int)StudyTime.TotalHours}시간 {StudyTime.Minutes}분";
    }
}