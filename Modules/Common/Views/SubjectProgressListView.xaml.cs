using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Common.ViewModels;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Daily.Views;
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

        // 🆕 삭제 버튼 클릭 이벤트
        private void RemoveSubject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SubjectProgressViewModel subjectToRemove)
            {
                try
                {
                    // 현재 바인딩된 컬렉션에서 제거
                    if (DataContext is LeftSidebarViewModel sidebarVM)
                    {
                        // 공유 데이터가 있으면 그것에서 제거
                        if (sidebarVM.SharedSubjectProgress != null)
                        {
                            var existingSubject = sidebarVM.SharedSubjectProgress.FirstOrDefault(s =>
                                string.Equals(s.SubjectName, subjectToRemove.SubjectName, StringComparison.OrdinalIgnoreCase));

                            if (existingSubject != null)
                            {
                                sidebarVM.SharedSubjectProgress.Remove(existingSubject);
                                System.Diagnostics.Debug.WriteLine($"[SubjectProgressListView] 공유 데이터에서 과목 '{subjectToRemove.SubjectName}' 제거됨");
                            }
                        }
                        // 공유 데이터가 없으면 Subjects에서 제거
                        else if (sidebarVM.Subjects != null)
                        {
                            // LeftSidebarViewModel의 Subjects는 SubjectGroupViewModel 타입이므로 별도 처리 필요
                            System.Diagnostics.Debug.WriteLine($"[SubjectProgressListView] 기본 Subjects는 다른 타입이므로 제거하지 않음");
                        }
                    }
                    // DailyBodyViewModel에서 직접 접근하는 경우
                    else if (DataContext is DailyBodyViewModel dailyVM)
                    {
                        var existingSubject = dailyVM.Subjects.FirstOrDefault(s =>
                            string.Equals(s.SubjectName, subjectToRemove.SubjectName, StringComparison.OrdinalIgnoreCase));

                        if (existingSubject != null)
                        {
                            dailyVM.Subjects.Remove(existingSubject);
                            System.Diagnostics.Debug.WriteLine($"[SubjectProgressListView] DailyVM에서 과목 '{subjectToRemove.SubjectName}' 제거됨");
                        }
                    }

                    // DB에서도 제거 (오늘 할 일에서만)
                    var dbHelper = DatabaseHelper.Instance;
                    dbHelper.RemoveDailySubject(DateTime.Today, subjectToRemove.SubjectName);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SubjectProgressListView] 과목 제거 오류: {ex.Message}");
                }
            }
        }

        // 중앙에서 좌측으로 드래그하기 위한 이벤트
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

                if (System.Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var textBlock = sender as TextBlock;
                    var subject = textBlock?.DataContext as SubjectProgressViewModel;

                    if (subject != null)
                    {
                        var dragData = new DataObject("RemoveSubjectData", subject);
                        DragDrop.DoDragDrop(textBlock, dragData, DragDropEffects.Move);
                    }

                    _isDragging = false;
                }
            }
        }

        private void SubjectProgressListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("SubjectData") || e.Data.GetDataPresent("RemoveSubjectData"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            DropTargetBorder.Tag = null;
            e.Handled = true;
        }

        private void SubjectProgressListView_Drop(object sender, DragEventArgs e)
        {
            DropTargetBorder.Tag = null;
            System.Diagnostics.Debug.WriteLine("[DragDrop] Drop 이벤트 발생");

            try
            {
                // 🆕 공유 데이터 소스 찾기
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

                // 🆕 제거 데이터 처리 (다른 곳에서 드래그해서 여기로 가져와 제거)
                if (e.Data.GetDataPresent("RemoveSubjectData"))
                {
                    var subjectToRemove = e.Data.GetData("RemoveSubjectData") as SubjectProgressViewModel;

                    if (subjectToRemove != null)
                    {
                        var existingSubject = targetCollection.FirstOrDefault(s =>
                            string.Equals(s.SubjectName, subjectToRemove.SubjectName, StringComparison.OrdinalIgnoreCase));

                        if (existingSubject != null)
                        {
                            targetCollection.Remove(existingSubject);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 드래그로 과목 '{subjectToRemove.SubjectName}' 제거됨");

                            // DB에서도 제거
                            var dbHelper = DatabaseHelper.Instance;
                            dbHelper.RemoveDailySubject(DateTime.Today, subjectToRemove.SubjectName);
                        }
                    }
                }
                // 추가 데이터 처리 (좌측에서 드래그해서 추가)
                else if (e.Data.GetDataPresent("SubjectData"))
                {
                    var droppedSubject = e.Data.GetData("SubjectData") as SubjectGroupViewModel;
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 드롭된 과목: {droppedSubject?.SubjectName}");

                    if (droppedSubject != null)
                    {
                        // 중복 체크
                        var existingSubject = targetCollection.FirstOrDefault(s =>
                            string.Equals(s.SubjectName, droppedSubject.SubjectName, StringComparison.OrdinalIgnoreCase));

                        if (existingSubject == null)
                        {
                            var newSubjectProgress = new SubjectProgressViewModel
                            {
                                SubjectName = droppedSubject.SubjectName,
                                Progress = 0.0,
                                StudyTimeMinutes = 0
                            };

                            targetCollection.Add(newSubjectProgress);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{droppedSubject.SubjectName}' 추가됨");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{droppedSubject.SubjectName}' 이미 존재함");
                        }
                    }
                }
                else if (e.Data.GetDataPresent("TopicData"))
                {
                    var droppedTopic = e.Data.GetData("TopicData") as TopicGroupViewModel;
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 드롭된 분류: {droppedTopic?.GroupTitle} (부모: {droppedTopic?.ParentSubjectName})");

                    if (droppedTopic != null && !string.IsNullOrEmpty(droppedTopic.ParentSubjectName))
                    {
                        // 부모 과목이 이미 있는지 확인
                        var existingSubject = targetCollection.FirstOrDefault(s =>
                            string.Equals(s.SubjectName, droppedTopic.ParentSubjectName, StringComparison.OrdinalIgnoreCase));

                        if (existingSubject != null)
                        {
                            // 기존 과목에 분류 추가
                            var existingTopic = existingSubject.TopicGroups.FirstOrDefault(t =>
                                string.Equals(t.GroupTitle, droppedTopic.GroupTitle, StringComparison.OrdinalIgnoreCase));

                            if (existingTopic == null)
                            {
                                existingSubject.TopicGroups.Add(droppedTopic);
                                System.Diagnostics.Debug.WriteLine($"[DragDrop] 기존 과목 '{droppedTopic.ParentSubjectName}'에 분류 '{droppedTopic.GroupTitle}' 추가됨");
                            }
                        }
                        else
                        {
                            // 새 과목과 분류 함께 추가
                            var newSubjectProgress = new SubjectProgressViewModel
                            {
                                SubjectName = droppedTopic.ParentSubjectName,
                                Progress = 0.0,
                                StudyTimeMinutes = 0
                            };
                            newSubjectProgress.TopicGroups.Add(droppedTopic);

                            targetCollection.Add(newSubjectProgress);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 분류와 함께 과목 '{droppedTopic.ParentSubjectName}' 추가됨");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DragDrop] 오류 발생: {ex.Message}");
            }

            e.Handled = true;
        }
    }
}