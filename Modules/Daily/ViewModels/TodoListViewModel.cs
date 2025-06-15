using SP.Modules.Daily.Models;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using SP.Modules.Common.Helpers;

namespace SP.Modules.Daily.ViewModels
{
    public class TodoListViewModel : ViewModelBase
    {
        private readonly DatabaseHelper _db = new();
        public ObservableCollection<TodoItem> Todos { get; set; }
        public ObservableCollection<TodoItem> TodoList { get; set; } = new();

        public string _newTodoText;
        public string NewTodoText
        {
            get => _newTodoText;
            set => SetProperty(ref _newTodoText, value);
        }
        private bool _isAdding;
        public bool IsAdding
        {
            get => _isAdding;
            set => SetProperty(ref _isAdding, value);
        }
        public DateTime CurrentDate { get; set; }

        public ICommand StartAddCommand { get; }
        public ICommand AddTodoCommand { get; }
        public ICommand DeleteTodoCommand { get; }

        public TodoListViewModel() : this(DateTime.Today) { }
        public TodoListViewModel(DateTime date)
        {
            CurrentDate = date;
            Todos = new ObservableCollection<TodoItem>();

            StartAddCommand = new RelayCommand(() => IsAdding = true);

            AddTodoCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(NewTodoText))
                {
                    int newId = _db.AddTodo(CurrentDate, NewTodoText.Trim());
                    Todos.Add(new TodoItem { Id = newId, Title = NewTodoText.Trim(), IsCompleted = false });
                    NewTodoText = string.Empty;
                    IsAdding = false;
                }
            });

            DeleteTodoCommand = new RelayCommand<TodoItem>(DeleteTodo);

            // 초기 데이터 로드 (선택 사항)
            LoadInitialTodos();
        }

        private void DeleteTodo(TodoItem todo)
        {
            if (todo == null) return;
            _db.DeleteTodo(todo.Id);
            Todos.Remove(todo);
        }

        private void LoadInitialTodos()
        {
            var list = _db.GetTodosByDate(CurrentDate);
            Todos.Clear();
            foreach (var item in list)
                Todos.Add(item);
        }
    }
}
