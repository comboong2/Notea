using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Daily.Views;
using SP.Modules.Subjects.ViewModels;
using SP.ViewModels;

namespace SP.Modules.Common.Views
{
    public partial class SubjectListView : UserControl
    {
        private Point _startPoint;
        private bool _isDragging = false;

        public SubjectListView()
        {
            InitializeComponent();
        }
<<<<<<< HEAD
<<<<<<< Updated upstream
=======

        // 과목 드래그 이벤트
        private void SubjectGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SubjectGrid_MouseMove(object sender, MouseEventArgs e)
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
                    var subject = grid?.DataContext as SubjectGroupViewModel;

                    if (subject != null)
                    {
                        var dragData = new DataObject("SubjectData", subject);
                        DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Copy);
                    }

                    _isDragging = false;
                }
            }
        }

        // 분류(TopicGroup) 드래그 이벤트 추가
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
                        // 부모 과목명을 찾아서 설정
                        var parentGrid = FindParent<Grid>(grid);
                        while (parentGrid != null)
                        {
                            if (parentGrid.DataContext is SubjectGroupViewModel parentSubject)
                            {
                                topicGroup.ParentSubjectName = parentSubject.SubjectName;
                                break;
                            }
                            parentGrid = FindParent<Grid>(parentGrid);
                        }

                        var dragData = new DataObject("TopicData", topicGroup);
                        DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Copy);
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] 분류 '{topicGroup.GroupTitle}' 드래그 시작 (부모: {topicGroup.ParentSubjectName})");
                    }

                    _isDragging = false;
                }
            }
        }

        // 부모 요소 찾기 헬퍼 메소드
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        // 중앙에서 좌측으로 드래그된 과목을 받아서 삭제 처리
        private void SubjectList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RemoveSubjectData"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void SubjectList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RemoveSubjectData"))
            {
                var subjectToRemove = e.Data.GetData("RemoveSubjectData") as SubjectProgressViewModel;

                if (subjectToRemove != null)
                {
                    // MainViewModel을 통해 DailyBodyViewModel에 접근
                    if (Window.GetWindow(this)?.DataContext is MainViewModel mainVM)
                    {
                        if (mainVM.BodyContent is DailyBodyView dailyBodyView &&
                            dailyBodyView.DataContext is DailyBodyViewModel dailyBodyVM)
                        {
                            // DB에서도 삭제
                            var dbHelper = DatabaseHelper.Instance;
                            //dbHelper.RemoveDailySubject(dailyBodyVM.SelectedDate, subjectToRemove.SubjectName);

                            // UI에서 제거
                            dailyBodyVM.Subjects.Remove(subjectToRemove);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{subjectToRemove.SubjectName}' 제거됨");
                        }
                    }
                }
            }
            e.Handled = true;
        }
>>>>>>> Stashed changes
    }
}
=======
>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244

        // 과목 드래그 이벤트
        private void SubjectGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SubjectGrid_MouseMove(object sender, MouseEventArgs e)
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
                    var subject = grid?.DataContext as SubjectGroupViewModel;

                    if (subject != null)
                    {
                        var dragData = new DataObject("SubjectData", subject);
                        DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Copy);
                    }

                    _isDragging = false;
                }
            }
        }

        // 분류(TopicGroup) 드래그 이벤트 추가
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
                        // 부모 과목명을 찾아서 설정
                        var parentGrid = FindParent<Grid>(grid);
                        while (parentGrid != null)
                        {
                            if (parentGrid.DataContext is SubjectGroupViewModel parentSubject)
                            {
                                topicGroup.ParentSubjectName = parentSubject.SubjectName;
                                break;
                            }
                            parentGrid = FindParent<Grid>(parentGrid);
                        }

                        var dragData = new DataObject("TopicData", topicGroup);
                        DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Copy);
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] 분류 '{topicGroup.GroupTitle}' 드래그 시작 (부모: {topicGroup.ParentSubjectName})");
                    }

                    _isDragging = false;
                }
            }
        }

        // 부모 요소 찾기 헬퍼 메소드
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        // 중앙에서 좌측으로 드래그된 과목을 받아서 삭제 처리
        private void SubjectList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RemoveSubjectData"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void SubjectList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RemoveSubjectData"))
            {
                var subjectToRemove = e.Data.GetData("RemoveSubjectData") as SubjectProgressViewModel;

                if (subjectToRemove != null)
                {
                    // MainViewModel을 통해 DailyBodyViewModel에 접근
                    if (Window.GetWindow(this)?.DataContext is MainViewModel mainVM)
                    {
                        if (mainVM.BodyContent is DailyBodyView dailyBodyView &&
                            dailyBodyView.DataContext is DailyBodyViewModel dailyBodyVM)
                        {
                            // DB에서도 삭제
                            var dbHelper = DatabaseHelper.Instance;
                            dbHelper.RemoveDailySubject(dailyBodyVM.SelectedDate, subjectToRemove.SubjectName);

                            // UI에서 제거
                            dailyBodyVM.Subjects.Remove(subjectToRemove);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{subjectToRemove.SubjectName}' 제거됨");
                        }
                    }
                }
            }
            e.Handled = true;
        }
    }
}