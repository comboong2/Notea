using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Common.ViewModels;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Subjects.ViewModels;
using SP.ViewModels;

namespace SP.Modules.Common.Views
{
    public partial class SubjectProgressListView : UserControl
    {
        private Point _startPoint;
        private bool _isDragging = false;

        public SubjectProgressListView()
        {
            InitializeComponent();
        }

        public ObservableCollection<SubjectProgressViewModel> Subjects
        {
            get => (ObservableCollection<SubjectProgressViewModel>)GetValue(SubjectsProperty);
            set => SetValue(SubjectsProperty, value);
        }

        public static readonly DependencyProperty SubjectsProperty =
            DependencyProperty.Register("Subjects", typeof(ObservableCollection<SubjectProgressViewModel>), typeof(SubjectProgressListView), new PropertyMetadata(null));

        // 과목명 드래그 이벤트 (삭제용)
        private void SubjectName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SubjectName_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var textBlock = sender as TextBlock;
                    var subject = textBlock?.DataContext as SubjectProgressViewModel;

                    if (subject != null)
                    {
                        var dragData = new DataObject("RemoveSubjectData", subject);
                        DragDrop.DoDragDrop(textBlock, dragData, DragDropEffects.Move);
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{subject.SubjectName}' 삭제 드래그 시작");
                    }

                    _isDragging = false;
                }
            }
        }

        // 🆕 TopicGroup 드래그 이벤트 (삭제용)
        private void TopicGroup_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void TopicGroup_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var grid = sender as Grid;
                    var topicGroup = grid?.DataContext as TopicGroupViewModel;

                    if (topicGroup != null)
                    {
                        // 부모 과목명 찾기
                        var parentSubject = FindParentSubject(grid);
                        if (parentSubject != null)
                        {
                            topicGroup.ParentSubjectName = parentSubject.SubjectName;
                        }

                        var dragData = new DataObject("RemoveTopicGroupData", topicGroup);
                        DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Move);
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] TopicGroup '{topicGroup.GroupTitle}' 삭제 드래그 시작 (부모: {topicGroup.ParentSubjectName})");
                    }

                    _isDragging = false;
                }
            }
        }

        // 부모 과목 찾기 헬퍼 메소드
        private SubjectProgressViewModel FindParentSubject(DependencyObject child)
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is FrameworkElement element && element.DataContext is SubjectProgressViewModel subject)
                {
                    return subject;
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void SubjectProgressListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("SubjectData") ||
                e.Data.GetDataPresent("TopicData") ||
                e.Data.GetDataPresent("RemoveSubjectData") ||
                e.Data.GetDataPresent("RemoveTopicGroupData"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void SubjectProgressListView_Drop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[DragDrop] Drop 이벤트 발생");

            try
            {
                // 공유 데이터 소스 찾기
                ObservableCollection<SubjectProgressViewModel> targetCollection = null;

                if (DataContext is LeftSidebarViewModel sidebarVM && sidebarVM.SharedSubjectProgress != null)
                {
                    targetCollection = sidebarVM.SharedSubjectProgress;
                }
                else if (DataContext is DailyBodyViewModel dailyBodyVM)
                {
                    targetCollection = dailyBodyVM.Subjects;
                }

                if (targetCollection == null)
                {
                    System.Diagnostics.Debug.WriteLine("[DragDrop] 대상 컬렉션을 찾을 수 없음");
                    return;
                }

                // 과목 제거 처리
                if (e.Data.GetDataPresent("RemoveSubjectData"))
                {
                    HandleRemoveSubject(e, targetCollection);
                }
                // TopicGroup 제거 처리
                else if (e.Data.GetDataPresent("RemoveTopicGroupData"))
                {
                    HandleRemoveTopicGroup(e, targetCollection);
                }
                // 과목 추가 처리 (전체 과목이 아닌 해당 과목만)
                else if (e.Data.GetDataPresent("SubjectData"))
                {
                    HandleAddSingleSubject(e, targetCollection);
                }
                // TopicGroup 추가 처리 (해당 TopicGroup만)
                else if (e.Data.GetDataPresent("TopicData"))
                {
                    HandleAddSingleTopicGroup(e, targetCollection);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DragDrop] 오류 발생: {ex.Message}");
            }

            e.Handled = true;
        }

        // 과목 제거 (전체 과목 삭제)
        private void HandleRemoveSubject(DragEventArgs e, ObservableCollection<SubjectProgressViewModel> targetCollection)
        {
            var subjectToRemove = e.Data.GetData("RemoveSubjectData") as SubjectProgressViewModel;

            if (subjectToRemove != null)
            {
                var existingSubject = targetCollection.FirstOrDefault(s =>
                    string.Equals(s.SubjectName, subjectToRemove.SubjectName, StringComparison.OrdinalIgnoreCase));

                if (existingSubject != null)
                {
                    targetCollection.Remove(existingSubject);
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{subjectToRemove.SubjectName}' 전체 제거됨");

                    var dbHelper = DatabaseHelper.Instance;
                    dbHelper.RemoveDailySubject(DateTime.Today, subjectToRemove.SubjectName);
                }
            }
        }

        // TopicGroup 제거 (해당 TopicGroup만 삭제, 부모 과목은 유지)
        private void HandleRemoveTopicGroup(DragEventArgs e, ObservableCollection<SubjectProgressViewModel> targetCollection)
        {
            var topicGroupToRemove = e.Data.GetData("RemoveTopicGroupData") as TopicGroupViewModel;

            if (topicGroupToRemove != null && !string.IsNullOrEmpty(topicGroupToRemove.ParentSubjectName))
            {
                var parentSubject = targetCollection.FirstOrDefault(s =>
                    string.Equals(s.SubjectName, topicGroupToRemove.ParentSubjectName, StringComparison.OrdinalIgnoreCase));

                if (parentSubject != null)
                {
                    var existingTopicGroup = parentSubject.TopicGroups.FirstOrDefault(t =>
                        string.Equals(t.GroupTitle, topicGroupToRemove.GroupTitle, StringComparison.OrdinalIgnoreCase));

                    if (existingTopicGroup != null)
                    {
                        parentSubject.TopicGroups.Remove(existingTopicGroup);
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] TopicGroup '{topicGroupToRemove.GroupTitle}' 제거됨 (부모: {topicGroupToRemove.ParentSubjectName})");

                        // 🔄 부모 과목은 TopicGroup이 없어도 유지
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] 부모 과목 '{parentSubject.SubjectName}' 유지됨 (TopicGroup 개수: {parentSubject.TopicGroups.Count})");
                    }
                }
            }
        }

        // 단일 과목 추가 (해당 과목만, TopicGroups 포함하지 않음)
        private void HandleAddSingleSubject(DragEventArgs e, ObservableCollection<SubjectProgressViewModel> targetCollection)
        {
            var droppedSubject = e.Data.GetData("SubjectData") as SubjectGroupViewModel;
            System.Diagnostics.Debug.WriteLine($"[DragDrop] 드롭된 과목: {droppedSubject?.SubjectName}");

            if (droppedSubject != null)
            {
                var existingSubject = targetCollection.FirstOrDefault(s =>
                    string.Equals(s.SubjectName, droppedSubject.SubjectName, StringComparison.OrdinalIgnoreCase));

                if (existingSubject == null)
                {
                    var newSubjectProgress = new SubjectProgressViewModel
                    {
                        SubjectName = droppedSubject.SubjectName,
                        Progress = 0.1, // 테스트용 초기값
                        StudyTimeMinutes = 30 // 테스트용 초기값
                    };

                    // 🔄 TopicGroups는 추가하지 않음 (과목만 추가)
                    targetCollection.Add(newSubjectProgress);
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{droppedSubject.SubjectName}' 단독 추가됨");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{droppedSubject.SubjectName}' 이미 존재함");
                }
            }
        }

        // 단일 TopicGroup 추가 (상위 과목이 없으면 과목도 함께 생성, Topics는 제외)
        private void HandleAddSingleTopicGroup(DragEventArgs e, ObservableCollection<SubjectProgressViewModel> targetCollection)
        {
            var droppedTopic = e.Data.GetData("TopicData") as TopicGroupViewModel;
            var parentSubjectName = e.Data.GetData("ParentSubjectName") as string;

            System.Diagnostics.Debug.WriteLine($"[DragDrop] 드롭된 분류: {droppedTopic?.GroupTitle}");
            System.Diagnostics.Debug.WriteLine($"[DragDrop] 부모 과목명: {parentSubjectName ?? droppedTopic?.ParentSubjectName}");

            if (droppedTopic != null)
            {
                var effectiveParentName = parentSubjectName ?? droppedTopic.ParentSubjectName;

                if (!string.IsNullOrEmpty(effectiveParentName))
                {
                    var existingSubject = targetCollection.FirstOrDefault(s =>
                        string.Equals(s.SubjectName, effectiveParentName, StringComparison.OrdinalIgnoreCase));

                    if (existingSubject != null)
                    {
                        // ✅ 기존 과목에 TopicGroup 추가 (중복 체크)
                        var existingTopic = existingSubject.TopicGroups.FirstOrDefault(t =>
                            string.Equals(t.GroupTitle, droppedTopic.GroupTitle, StringComparison.OrdinalIgnoreCase));

                        if (existingTopic == null)
                        {
                            // ✅ 새로운 TopicGroup 생성
                            var newTopicGroup = new TopicGroupViewModel
                            {
                                GroupTitle = droppedTopic.GroupTitle,
                                ParentSubjectName = effectiveParentName,
                                TodayStudyTimeSeconds = 0, // ✅ 수정: 초기값 0초
                                Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>() // 빈 컬렉션
                            };

                            // ✅ 수정: 부모의 오늘 학습시간 설정 (올바른 메소드 사용)
                            newTopicGroup.SetParentTodayStudyTime(existingSubject.TodayStudyTimeSeconds);

                            existingSubject.TopicGroups.Add(newTopicGroup);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 기존 과목 '{effectiveParentName}'에 TopicGroup '{droppedTopic.GroupTitle}' 추가됨 (부모 오늘시간: {existingSubject.TodayStudyTimeSeconds}초)");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] TopicGroup '{droppedTopic.GroupTitle}'가 이미 존재함");
                        }
                    }
                    else
                    {
                        // ✅ 새 과목과 TopicGroup 함께 추가
                        var newSubjectProgress = new SubjectProgressViewModel
                        {
                            SubjectName = effectiveParentName,
                            Progress = 0.0, // ✅ 수정: 0.0으로 초기화
                            TodayStudyTimeSeconds = 3600 // ✅ 수정: 기본값 1시간 (3600초)
                        };

                        // ✅ 새로운 TopicGroup 생성
                        var newTopicGroup = new TopicGroupViewModel
                        {
                            GroupTitle = droppedTopic.GroupTitle,
                            ParentSubjectName = effectiveParentName,
                            TodayStudyTimeSeconds = 0, // ✅ 수정: 초기값 0초
                            Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>() // 빈 컬렉션
                        };

                        // ✅ 수정: 부모의 오늘 학습시간 설정
                        newTopicGroup.SetParentTodayStudyTime(newSubjectProgress.TodayStudyTimeSeconds);

                        newSubjectProgress.TopicGroups.Add(newTopicGroup);
                        targetCollection.Add(newSubjectProgress);
                    }
                }
            }
        }
    }
}