using SP.Modules.Common.Models;
using SP.Modules.Daily.Models;
using System;
using System.Data.SQLite;
using System.IO;
using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;  // ← 반드시 필요


namespace SP.Modules.Common.Helpers
{
<<<<<<< Updated upstream
    private readonly string _dbPath;

    public DatabaseHelper()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _dbPath = Path.Combine(baseDir, "notea.db");

        Initialize();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection($"Data Source={_dbPath};Version=3;");
    }

    public void Initialize()
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Note (
                    NoteId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Content TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ";
        cmd.ExecuteNonQuery();

        // ⬇ Comment 테이블 생성 추가
        cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS Comment (
        Date TEXT PRIMARY KEY,
        Text TEXT NOT NULL
    );
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS Todo (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Date TEXT NOT NULL,
        Title TEXT NOT NULL,
        IsCompleted INTEGER NOT NULL DEFAULT 0
    );
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS Subject (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL UNIQUE,
        TotalStudyTime INTEGER NOT NULL DEFAULT 0
    );";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS TopicGroup (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        SubjectId INTEGER NOT NULL,
        Name TEXT NOT NULL,
        TotalStudyTime INTEGER NOT NULL DEFAULT 0,
        FOREIGN KEY (SubjectId) REFERENCES Subject(Id) ON DELETE CASCADE
    );";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS TopicItem (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        TopicGroupId INTEGER NOT NULL,
        Content TEXT NOT NULL,
        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY (TopicGroupId) REFERENCES TopicGroup(Id) ON DELETE CASCADE
    );";
        cmd.ExecuteNonQuery();
    }
    public List<Note> GetAllNotes()
    {
        var list = new List<Note>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Note ORDER BY UpdatedAt DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Note
            {
                NoteId = Convert.ToInt32(reader["NoteId"]),
                Content = reader["Content"].ToString(),
                CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString())
            });
        }
        return list;
    }

    public void SaveNote(Note note)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();

        if (note.NoteId == 0)
        {
            cmd.CommandText = "INSERT INTO Note (Content) VALUES (@content)";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE Note 
                SET Content = @content, UpdatedAt = CURRENT_TIMESTAMP 
                WHERE NoteId = @noteId";
            cmd.Parameters.AddWithValue("@noteId", note.NoteId);
        }

        cmd.Parameters.AddWithValue("@content", note.Content);
        cmd.ExecuteNonQuery();
    }

    public void DeleteNote(int noteId)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Note WHERE NoteId = @id";
        cmd.Parameters.AddWithValue("@id", noteId);
        cmd.ExecuteNonQuery();

    }


    public void UpdateNote(Note note)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE note SET content = @content, updatedAt = CURRENT_TIMESTAMP WHERE noteId = @id";
        cmd.Parameters.AddWithValue("@content", note.Content);
        cmd.Parameters.AddWithValue("@id", note.NoteId);
        cmd.ExecuteNonQuery();
    }

    public string GetCommentByDate(DateTime date)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Text FROM Comment WHERE Date = @date";
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? string.Empty;
    }

    public void SaveOrUpdateComment(DateTime date, string text)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO Comment (Date, Text)
        VALUES (@date, @text)
        ON CONFLICT(Date)
        DO UPDATE SET Text = @text
    ";
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@text", text);
        cmd.ExecuteNonQuery();
    }

    public List<TodoItem> GetTodosByDate(DateTime date)
    {
        var list = new List<TodoItem>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Todo WHERE Date = @date ORDER BY Id";
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TodoItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                Title = reader["Title"].ToString(),
                IsCompleted = Convert.ToInt32(reader["IsCompleted"]) == 1
            });
        }
        return list;
    }

    public int AddTodo(DateTime date, string title)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Todo (Date, Title, IsCompleted) VALUES (@date, @title, 0); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@title", title);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }


    public void UpdateTodoCompletion(int id, bool isCompleted)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Todo SET IsCompleted = @done WHERE Id = @id";
        cmd.Parameters.AddWithValue("@done", isCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteTodo(int id)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Todo WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public int AddSubject(string name)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Subject (Name) VALUES (@name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int AddTopicGroup(int subjectId, string name)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO TopicGroup (SubjectId, Name) VALUES (@subjectId, @name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@subjectId", subjectId);
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int AddTopicItem(int topicGroupId, string content)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO TopicItem (TopicGroupId, Content) VALUES (@groupId, @content); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", topicGroupId);
        cmd.Parameters.AddWithValue("@content", content);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateSubjectStudyTime(int subjectId, int second)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Subject SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
        cmd.Parameters.AddWithValue("@sec", second);
        cmd.Parameters.AddWithValue("@id", subjectId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateTopicGroupStudyTime(int groupId, int second)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE TopicGroup SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
        cmd.Parameters.AddWithValue("@sec", second);
        cmd.Parameters.AddWithValue("@id", groupId);
        cmd.ExecuteNonQuery();
    }


    public List<SubjectGroupViewModel> LoadSubjectsWithGroups()
    {
        var result = new List<SubjectGroupViewModel>();

        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Subject";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var subjectId = Convert.ToInt32(reader["Id"]);
            var subjectName = reader["Name"].ToString();

            var subjectVM = new SubjectGroupViewModel
            {
                SubjectId = subjectId,
                SubjectName = subjectName,
                TopicGroups = new ObservableCollection<TopicGroupViewModel>()
            };

            // 불러올 경우: 하위 TopicGroup도 포함
            using var groupCmd = conn.CreateCommand();
            groupCmd.CommandText = "SELECT Id, Name FROM TopicGroup WHERE SubjectId = @id";
            groupCmd.Parameters.AddWithValue("@id", subjectId);
            using var groupReader = groupCmd.ExecuteReader();

            while (groupReader.Read())
            {
                subjectVM.TopicGroups.Add(new TopicGroupViewModel
                {
                    GroupTitle = groupReader["Name"].ToString(),
                    Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>()
                });
            }

            result.Add(subjectVM);
        }

        return result;
    }






}
=======
    public class DatabaseHelper
    {
        private static volatile DatabaseHelper _instance;
        private static readonly object _lock = new object();
        private readonly SQLiteConnection _connection;
        private readonly string _dbPath;

        // 싱글톤 인스턴스 접근
        public static DatabaseHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new DatabaseHelper();
                    }
                }
                return _instance;
            }
        }

        private DatabaseHelper()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "notea.db");

            _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;Pooling=false;");
            _connection.Open();

            Initialize();
        }

        private void Initialize()
        {
            lock (_lock)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Note (
                        NoteId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Content TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                ";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Comment (
                        Date TEXT PRIMARY KEY,
                        Text TEXT NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Todo (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Date TEXT NOT NULL,
                        Title TEXT NOT NULL,
                        IsCompleted INTEGER NOT NULL DEFAULT 0
                    );
                ";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Subject (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        TotalStudyTime INTEGER NOT NULL DEFAULT 0
                    );";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS TopicGroup (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SubjectId INTEGER NOT NULL,
                        Name TEXT NOT NULL,
                        TotalStudyTime INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY (SubjectId) REFERENCES Subject(Id) ON DELETE CASCADE
                    );";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS TopicItem (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TopicGroupId INTEGER NOT NULL,
                        Content TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (TopicGroupId) REFERENCES TopicGroup(Id) ON DELETE CASCADE
                    );";
                cmd.ExecuteNonQuery();
            }
        }

        // Note 관련 메서드들
        public List<Note> GetAllNotes()
        {
            lock (_lock)
            {
                var list = new List<Note>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Note ORDER BY UpdatedAt DESC";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new Note
                    {
                        NoteId = Convert.ToInt32(reader["NoteId"]),
                        Content = reader["Content"].ToString(),
                        CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                        UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString())
                    });
                }
                return list;
            }
        }

        public void SaveNote(Note note)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();

                if (note.NoteId == 0)
                {
                    cmd.CommandText = "INSERT INTO Note (Content) VALUES (@content)";
                }
                else
                {
                    cmd.CommandText = @"
                        UPDATE Note 
                        SET Content = @content, UpdatedAt = CURRENT_TIMESTAMP 
                        WHERE NoteId = @noteId";
                    cmd.Parameters.AddWithValue("@noteId", note.NoteId);
                }

                cmd.Parameters.AddWithValue("@content", note.Content);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteNote(int noteId)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Note WHERE NoteId = @id";
                cmd.Parameters.AddWithValue("@id", noteId);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateNote(Note note)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "UPDATE note SET content = @content, updatedAt = CURRENT_TIMESTAMP WHERE noteId = @id";
                cmd.Parameters.AddWithValue("@content", note.Content);
                cmd.Parameters.AddWithValue("@id", note.NoteId);
                cmd.ExecuteNonQuery();
            }
        }

        // Comment 관련 메서드들
        public string GetCommentByDate(DateTime date)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Text FROM Comment WHERE Date = @date";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? string.Empty;
            }
        }

        public void SaveOrUpdateComment(DateTime date, string text)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Comment (Date, Text)
                    VALUES (@date, @text)
                    ON CONFLICT(Date)
                    DO UPDATE SET Text = @text
                ";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@text", text);
                cmd.ExecuteNonQuery();
            }
        }

        // Todo 관련 메서드들
        public List<TodoItem> GetTodosByDate(DateTime date)
        {
            lock (_lock)
            {
                var list = new List<TodoItem>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Todo WHERE Date = @date ORDER BY Id";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new TodoItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Title = reader["Title"].ToString(),
                        IsCompleted = Convert.ToInt32(reader["IsCompleted"]) == 1
                    });
                }
                return list;
            }
        }

        public int AddTodo(DateTime date, string title)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO Todo (Date, Title, IsCompleted) VALUES (@date, @title, 0); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@title", title);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void UpdateTodoCompletion(int id, bool isCompleted)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "UPDATE Todo SET IsCompleted = @done WHERE Id = @id";
                cmd.Parameters.AddWithValue("@done", isCompleted ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteTodo(int id)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Todo WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        // Subject 관련 메서드들
        public int AddSubject(string name)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO Subject (Name) VALUES (@name); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@name", name);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int AddTopicGroup(int subjectId, string name)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO TopicGroup (SubjectId, Name) VALUES (@subjectId, @name); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@subjectId", subjectId);
                cmd.Parameters.AddWithValue("@name", name);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int AddTopicItem(int topicGroupId, string content)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO TopicItem (TopicGroupId, Content) VALUES (@groupId, @content); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@groupId", topicGroupId);
                cmd.Parameters.AddWithValue("@content", content);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void UpdateSubjectStudyTime(int subjectId, int second)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "UPDATE Subject SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
                cmd.Parameters.AddWithValue("@sec", second);
                cmd.Parameters.AddWithValue("@id", subjectId);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateTopicGroupStudyTime(int groupId, int second)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "UPDATE TopicGroup SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
                cmd.Parameters.AddWithValue("@sec", second);
                cmd.Parameters.AddWithValue("@id", groupId);
                cmd.ExecuteNonQuery();
            }
        }

        // 과목과 토픽 그룹을 함께 로드하는 메서드 (기존)
        public List<SubjectGroupViewModel> LoadSubjectsWithGroups()
        {
            lock (_lock)
            {
                var result = new List<SubjectGroupViewModel>();

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Id, Name, TotalStudyTime FROM Subject ORDER BY Name";
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var subjectId = Convert.ToInt32(reader["Id"]);
                    var subjectName = reader["Name"].ToString();
                    var totalStudyTime = Convert.ToInt32(reader["TotalStudyTime"]);

                    var subjectVM = new SubjectGroupViewModel
                    {
                        SubjectId = subjectId,
                        SubjectName = subjectName,
                        TotalStudyTime = totalStudyTime,
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                    };

                    result.Add(subjectVM);
                }

                // 각 과목에 대해 토픽 그룹 로드
                foreach (var subject in result)
                {
                    LoadTopicGroupsForSubject(subject);
                }

                return result;
            }
        }

        // ✅ 새로 추가: 학습시간 정보와 함께 과목을 로드하는 메서드
        public List<SubjectGroupViewModel> LoadSubjectsWithStudyTime()
        {
            lock (_lock)
            {
                var result = new List<SubjectGroupViewModel>();

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Id, Name, TotalStudyTime FROM Subject ORDER BY TotalStudyTime DESC";
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var subjectId = Convert.ToInt32(reader["Id"]);
                    var subjectName = reader["Name"].ToString();
                    var totalStudyTime = Convert.ToInt32(reader["TotalStudyTime"]);

                    var subjectVM = new SubjectGroupViewModel
                    {
                        SubjectId = subjectId,
                        SubjectName = subjectName,
                        TotalStudyTime = totalStudyTime,
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                    };

                    result.Add(subjectVM);
                }

                return result;
            }
        }

        private void LoadTopicGroupsForSubject(SubjectGroupViewModel subject)
        {
            using var groupCmd = _connection.CreateCommand();
            groupCmd.CommandText = "SELECT Id, Name, TotalStudyTime FROM TopicGroup WHERE SubjectId = @id ORDER BY Name";
            groupCmd.Parameters.AddWithValue("@id", subject.SubjectId);

            using var groupReader = groupCmd.ExecuteReader();
            while (groupReader.Read())
            {
                var groupId = Convert.ToInt32(groupReader["Id"]);
                var groupName = groupReader["Name"].ToString();
                var groupStudyTime = Convert.ToInt32(groupReader["TotalStudyTime"]);

                var topicGroup = new TopicGroupViewModel
                {
                    GroupTitle = groupName,
                    TotalStudyTime = groupStudyTime,
                    Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>()
                };

                // 과목의 총 학습시간 설정 (비율 계산용)
                topicGroup.SetSubjectTotalTime(subject.TotalStudyTime);

                subject.TopicGroups.Add(topicGroup);
            }
        }

        // ✅ 새로 추가: 전체 과목의 총 학습시간을 반환하는 메서드
        public int GetTotalAllSubjectsStudyTime()
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COALESCE(SUM(TotalStudyTime), 0) FROM Subject";
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        // 특정 과목의 학습시간을 반환하는 메서드
        public int GetSubjectStudyTime(int subjectId)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT TotalStudyTime FROM Subject WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", subjectId);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
        }

        // ✅ 누락된 메서드들 추가 (올바른 시그니처)

        // 학습 세션 저장 - 오버로드 추가
        public void SaveStudySession(int subjectId, int studyTimeMinutes, DateTime sessionDate)
        {
            lock (_lock)
            {
                // 학습시간을 초 단위로 변환하여 저장
                int studyTimeSeconds = studyTimeMinutes * 60;
                UpdateSubjectStudyTime(subjectId, studyTimeSeconds);
            }
        }

        // 다른 시그니처 오버로드
        public void SaveStudySession(DateTime date, DateTime endTime, int subjectId)
        {
            lock (_lock)
            {
                // 시간 차이를 초 단위로 계산
                int studyTimeSeconds = (int)(endTime - date).TotalSeconds;
                UpdateSubjectStudyTime(subjectId, studyTimeSeconds);
            }
        }

        // 총 학습시간을 분 단위로 반환 - 오버로드 추가
        public int GetTotalStudyTimeMinutes()
        {
            lock (_lock)
            {
                int totalSeconds = GetTotalAllSubjectsStudyTime();
                return totalSeconds / 60; // 초를 분으로 변환
            }
        }

        // 특정 과목의 학습시간을 분 단위로 반환
        public int GetTotalStudyTimeMinutes(int subjectId)
        {
            lock (_lock)
            {
                int totalSeconds = GetSubjectStudyTime(subjectId);
                return totalSeconds / 60; // 초를 분으로 변환
            }
        }

        // 일일 과목 제거 - 오버로드 추가
        public void RemoveDailySubject(int subjectId)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Subject WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", subjectId);
                cmd.ExecuteNonQuery();
            }
        }

        // 날짜와 과목ID로 제거
        public void RemoveDailySubject(DateTime date, int subjectId)
        {
            lock (_lock)
            {
                // 특정 날짜의 할 일 중 특정 과목 관련 제거 (예시)
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Todo WHERE Date = @date AND Title LIKE @subject";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@subject", $"%{subjectId}%");
                cmd.ExecuteNonQuery();
            }
        }

        // 과목 삭제
        public void DeleteSubject(int subjectId)
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Subject WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", subjectId);
                cmd.ExecuteNonQuery();
            }
        }

        // 리소스 정리
        public void Dispose()
        {
            lock (_lock)
            {
                _connection?.Close();
                _connection?.Dispose();
            }
        }
    }
}
>>>>>>> Stashed changes
