using SP.Modules.Common.Models;
using SP.Modules.Daily.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;

namespace SP.Modules.Common.Helpers
{
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

                // StudySession 테이블 생성 (초 단위)
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

        // Note 관련 메소드들
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
                cmd.CommandText = "UPDATE Note SET Content = @content, UpdatedAt = CURRENT_TIMESTAMP WHERE NoteId = @id";
                cmd.Parameters.AddWithValue("@content", note.Content);
                cmd.Parameters.AddWithValue("@id", note.NoteId);
                cmd.ExecuteNonQuery();
            }
        }

        // Comment 관련 메소드들
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

        // Todo 관련 메소드들
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

        // Subject 관련 메소드들
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

        public void UpdateSubjectStudyTime(int subjectId, int seconds)
        {
            lock (_lockObject)
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Subject SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
                cmd.Parameters.AddWithValue("@sec", seconds);
                cmd.Parameters.AddWithValue("@id", subjectId);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateTopicGroupStudyTime(int groupId, int seconds)
        {
            lock (_lockObject)
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE TopicGroup SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
                cmd.Parameters.AddWithValue("@sec", seconds);
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
                    LoadTopicGroupsForSubject(conn, subject);
                }

                return result;
            }
        }

        private void LoadTopicGroupsForSubject(SQLiteConnection conn, SubjectGroupViewModel subject)
        {
            using var groupCmd = conn.CreateCommand();
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
                    ParentSubjectName = subject.SubjectName,
                    Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>()
                };

                // 과목의 총 학습시간 설정 (비율 계산용)
                topicGroup.SetSubjectTotalTime(subject.TotalStudyTime);

                subject.TopicGroups.Add(topicGroup);
            }
        }

        // 학습 시간 관련 메소드들 (초 단위 기준)
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

        // 호환성을 위한 분 단위 메소드들
        public int GetTotalStudyTimeMinutes(DateTime date)
        {
            var totalSeconds = GetTotalStudyTimeSeconds(date);
            return totalSeconds / 60;
        }

        public int GetTotalStudyTimeMinutes()
        {
            var totalSeconds = GetTotalStudyTimeSeconds();
            return totalSeconds / 60;
        }

        // 과목 학습시간 관련 메소드들
        public int GetTotalAllSubjectsStudyTime()
        {
            lock (_lockObject)
            {
                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COALESCE(SUM(TotalStudyTime), 0) FROM Subject";
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        public List<SubjectGroupViewModel> LoadSubjectsWithStudyTime()
        {
            lock (_lockObject)
            {
                var result = new List<SubjectGroupViewModel>();

                using var conn = GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
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

        // 오늘 할 일 과목 관련 메소드들
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

        // 특정 날짜의 모든 DailySubject 삭제
        public void RemoveAllDailySubjects(DateTime date)
        {
            lock (_lockObject)
            {
                try
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    cmd.ExecuteNonQuery();

                    System.Diagnostics.Debug.WriteLine($"[DB] 해당 날짜의 모든 오늘 할 일 과목 삭제: {date:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB] 모든 오늘 할 일 과목 삭제 오류: {ex.Message}");
                }
            }
        }

        // IDisposable 구현 (메모리 누수 방지)
        public void Dispose()
        {
            // 필요시 리소스 정리
        }
    }
}