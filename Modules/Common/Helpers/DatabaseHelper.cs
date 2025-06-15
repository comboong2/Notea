using SP.Modules.Common.Models;
using SP.Modules.Daily.Models;
using System;
using System.Data.SQLite;
using System.IO;
using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;

namespace SP.Modules.Common.Helpers;

public class DatabaseHelper
{
    private static DatabaseHelper _instance;
    private static readonly object _lockObject = new object();
    private readonly string _dbPath;

    // 싱글톤 패턴
    public static DatabaseHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
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
        Initialize();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection($"Data Source={_dbPath};Version=3;");
    }

    public void Initialize()
    {
        lock (_lockObject)
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

            // 기존 테이블이 있는지 확인하고 마이그레이션
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='StudySession';";
            var tableExists = cmd.ExecuteScalar() != null;

            if (tableExists)
            {
                // 기존 테이블 구조 확인
                cmd.CommandText = "PRAGMA table_info(StudySession);";
                using var reader = cmd.ExecuteReader();
                bool hasDurationSeconds = false;
                while (reader.Read())
                {
                    if (reader["name"].ToString() == "DurationSeconds")
                    {
                        hasDurationSeconds = true;
                        break;
                    }
                }
                reader.Close();

                if (!hasDurationSeconds)
                {
                    // 기존 테이블을 새 구조로 마이그레이션
                    System.Diagnostics.Debug.WriteLine("[DB] StudySession 테이블 마이그레이션 시작");

                    cmd.CommandText = @"
                        CREATE TABLE StudySession_new (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            StartTime TEXT NOT NULL,
                            EndTime TEXT NOT NULL,
                            DurationSeconds INTEGER NOT NULL,
                            Date TEXT NOT NULL,
                            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                        );";
                    cmd.ExecuteNonQuery();

                    // 기존 데이터 변환 (분 -> 초)
                    cmd.CommandText = @"
                        INSERT INTO StudySession_new (Id, StartTime, EndTime, DurationSeconds, Date, CreatedAt)
                        SELECT Id, StartTime, EndTime, DurationMinutes * 60, Date, CreatedAt
                        FROM StudySession;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE StudySession;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "ALTER TABLE StudySession_new RENAME TO StudySession;";
                    cmd.ExecuteNonQuery();

                    System.Diagnostics.Debug.WriteLine("[DB] StudySession 테이블 마이그레이션 완료");
                }
            }
            else
            {
                // 새 테이블 생성
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS StudySession (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        StartTime TEXT NOT NULL,
                        EndTime TEXT NOT NULL,
                        DurationSeconds INTEGER NOT NULL,
                        Date TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );";
                cmd.ExecuteNonQuery();
            }

            // 오늘 할 일 과목 리스트 테이블 추가
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS DailySubject (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    SubjectName TEXT NOT NULL,
                    Progress REAL NOT NULL DEFAULT 0.0,
                    StudyTimeMinutes INTEGER NOT NULL DEFAULT 0,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0
                );";
            cmd.ExecuteNonQuery();
        }
    }

    // 모든 DB 접근 메소드에 lock 적용
    public List<Note> GetAllNotes()
    {
        lock (_lockObject)
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
    }

    public void SaveNote(Note note)
    {
        lock (_lockObject)
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
    }

    public void DeleteNote(int noteId)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Note WHERE NoteId = @id";
            cmd.Parameters.AddWithValue("@id", noteId);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpdateNote(Note note)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE note SET content = @content, updatedAt = CURRENT_TIMESTAMP WHERE noteId = @id";
            cmd.Parameters.AddWithValue("@content", note.Content);
            cmd.Parameters.AddWithValue("@id", note.NoteId);
            cmd.ExecuteNonQuery();
        }
    }

    public string GetCommentByDate(DateTime date)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Text FROM Comment WHERE Date = @date";
            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? string.Empty;
        }
    }

    public void SaveOrUpdateComment(DateTime date, string text)
    {
        lock (_lockObject)
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
    }

    public List<TodoItem> GetTodosByDate(DateTime date)
    {
        lock (_lockObject)
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
    }

    public int AddTodo(DateTime date, string title)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Todo (Date, Title, IsCompleted) VALUES (@date, @title, 0); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@title", title);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void UpdateTodoCompletion(int id, bool isCompleted)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Todo SET IsCompleted = @done WHERE Id = @id";
            cmd.Parameters.AddWithValue("@done", isCompleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteTodo(int id)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Todo WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public int AddSubject(string name)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Subject (Name) VALUES (@name); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public int AddTopicGroup(int subjectId, string name)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO TopicGroup (SubjectId, Name) VALUES (@subjectId, @name); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@subjectId", subjectId);
            cmd.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public int AddTopicItem(int topicGroupId, string content)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO TopicItem (TopicGroupId, Content) VALUES (@groupId, @content); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@groupId", topicGroupId);
            cmd.Parameters.AddWithValue("@content", content);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void UpdateSubjectStudyTime(int subjectId, int second)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Subject SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
            cmd.Parameters.AddWithValue("@sec", second);
            cmd.Parameters.AddWithValue("@id", subjectId);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpdateTopicGroupStudyTime(int groupId, int second)
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE TopicGroup SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
            cmd.Parameters.AddWithValue("@sec", second);
            cmd.Parameters.AddWithValue("@id", groupId);
            cmd.ExecuteNonQuery();
        }
    }

    public List<SubjectGroupViewModel> LoadSubjectsWithGroups()
    {
        lock (_lockObject)
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

                // 하위 TopicGroup도 포함
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

    // 학습 세션 저장 - 초 단위로 변경
    public void SaveStudySession(DateTime startTime, DateTime endTime, int durationSeconds)
    {
        lock (_lockObject)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO StudySession (StartTime, EndTime, DurationSeconds, Date)
                    VALUES (@startTime, @endTime, @duration, @date)";

                var dateString = startTime.ToString("yyyy-MM-dd");
                cmd.Parameters.AddWithValue("@startTime", startTime.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@endTime", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@duration", durationSeconds);
                cmd.Parameters.AddWithValue("@date", dateString);

                cmd.ExecuteNonQuery();

                System.Diagnostics.Debug.WriteLine($"[DB] 학습 세션 저장 성공: {durationSeconds}초, 날짜: {dateString}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 학습 세션 저장 오류: {ex.Message}");
            }
        }
    }

    // 특정 날짜의 총 학습 시간 가져오기 (초 단위)
    public int GetTotalStudyTimeSeconds(DateTime date)
    {
        lock (_lockObject)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COALESCE(SUM(DurationSeconds), 0) FROM StudySession WHERE Date = @date";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                var result = cmd.ExecuteScalar();
                var totalSeconds = Convert.ToInt32(result);

                System.Diagnostics.Debug.WriteLine($"[DB] 날짜 {date:yyyy-MM-dd}의 총 학습시간: {totalSeconds}초");
                return totalSeconds;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 학습시간 조회 오류: {ex.Message}");
                return 0;
            }
        }
    }

    // 전체 학습 시간 가져오기 (초 단위)
    public int GetTotalStudyTimeSeconds()
    {
        lock (_lockObject)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(DurationSeconds), 0) FROM StudySession";

            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
    }

    // 호환성을 위해 기존 메소드도 유지 (분 단위 버전)
    public int GetTotalStudyTimeMinutes(DateTime date)
    {
        var totalSeconds = GetTotalStudyTimeSeconds(date);
        return totalSeconds / 60; // 초를 분으로 변환
    }

    public int GetTotalStudyTimeMinutes()
    {
        var totalSeconds = GetTotalStudyTimeSeconds();
        return totalSeconds / 60; // 초를 분으로 변환
    }

    // 오늘 할 일 과목 저장
    public void SaveDailySubject(DateTime date, string subjectName, double progress, int studyTimeMinutes, int displayOrder)
    {
        lock (_lockObject)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO DailySubject (Date, SubjectName, Progress, StudyTimeMinutes, DisplayOrder)
                    VALUES (@date, @subjectName, @progress, @studyTime, @order)";

                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@subjectName", subjectName);
                cmd.Parameters.AddWithValue("@progress", progress);
                cmd.Parameters.AddWithValue("@studyTime", studyTimeMinutes);
                cmd.Parameters.AddWithValue("@order", displayOrder);

                cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 저장: {subjectName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 저장 오류: {ex.Message}");
            }
        }
    }

    // 오늘 할 일 과목 리스트 로드
    public List<(string SubjectName, double Progress, int StudyTimeMinutes)> GetDailySubjects(DateTime date)
    {
        lock (_lockObject)
        {
            var result = new List<(string, double, int)>();
            try
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT SubjectName, Progress, StudyTimeMinutes FROM DailySubject WHERE Date = @date ORDER BY DisplayOrder";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add((
                        reader["SubjectName"].ToString(),
                        Convert.ToDouble(reader["Progress"]),
                        Convert.ToInt32(reader["StudyTimeMinutes"])
                    ));
                }

                System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 {result.Count}개 로드됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 로드 오류: {ex.Message}");
            }
            return result;
        }
    }

    // 오늘 할 일 과목 삭제
    public void RemoveDailySubject(DateTime date, string subjectName)
    {
        lock (_lockObject)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date AND SubjectName = @subjectName";
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@subjectName", subjectName);
                cmd.ExecuteNonQuery();

                System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 삭제: {subjectName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 삭제 오류: {ex.Message}");
            }
        }
    }
}