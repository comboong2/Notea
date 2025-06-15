using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SP.Modules.Daily.Models
{
    public class TodoItem : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    OnPropertyChanged();

                    //  DB에 저장
                    SP.Modules.Common.Helpers.DatabaseHelper helper = new();
                    helper.UpdateTodoCompletion(Id, _isCompleted);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
