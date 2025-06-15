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

        private readonly DatabaseHelper _db = new();

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

        public DailyBodyViewModel(DateTime appStartDate)
        {
            SelectedDate = appStartDate;

            Subjects = new ObservableCollection<SubjectProgressViewModel>
            {
                new SubjectProgressViewModel { SubjectName = "자료구조", Progress = 0.4 },
                new SubjectProgressViewModel { SubjectName = "인공지능", Progress = 0.7 }
            };

            TodoList = new ObservableCollection<TodoItem>();

            AddTodoCommand = new RelayCommand(AddTodo);
            StartAddCommand = new RelayCommand(() =>
            {
                IsAdding = true;
                RequestFocusOnInput?.Invoke();
            });

            // comment,TodoList 불러오기
            LoadDailyData(SelectedDate);
        }

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

                TodoList.Add(newItem);
                NewTodoText = string.Empty;
            }
            IsAdding = false;
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

            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] LoadDailyData 호출됨. 날짜: {date.ToShortDateString()}"); // ★★★ 디버그 출력 추가 ★★★

            // Comment 불러오기
            Comment = _db.GetCommentByDate(date);
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] Comment 로드됨: '{Comment}'"); // ★★★ 디버그 출력 추가 ★★★

            var todos = _db.GetTodosByDate(date);
            TodoList.Clear();
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 투두 항목 {todos.Count}개 DB에서 로드됨."); // ★★★ 디버그 출력 추가 ★★★

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
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] TodoList에 {TodoList.Count}개 항목 추가됨."); // ★★★ 디버그 출력 추가 ★★★
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
                    // TodoList = new ObservableCollection<TodoItem>(_db.GetTodosByDate(value)); // 추후 확장
                }
            }
        }

        public TimeSpan StudyTime { get; set; } = TimeSpan.FromMinutes(165);
        public int Dday => (TargetDate - DateTime.Today).Days;
        public DateTime TargetDate { get; set; } = new DateTime(2025, 6, 22);

        public string TotalStudyTime => $"{(int)StudyTime.TotalHours}시간 {StudyTime.Minutes}분";
    }
}
