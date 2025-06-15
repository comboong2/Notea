using SP.Modules.Common.Models;
using SP.Modules.Daily.Models;
using System;
using System.Data.SQLite;
using System.IO;
using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;  // ← 반드시 필요


namespace SP.Modules.Common.Helpers;

public class DatabaseHelper
{
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
