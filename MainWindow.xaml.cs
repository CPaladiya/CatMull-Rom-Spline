using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;



namespace PithExp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        //Pith labeling flag ON
        bool PithLabelingON = false; //Pith labeling mode ON
        bool PithCPointMoveInProg = false; //Control point moving and adjustment in progress by user
        int UserSelectCtrlPointIdx = -1; //Index of a currently selected cotrol point by user
        Ellipse UserSelectCtrlPointEllipse; //Ellipse of currently selected user control point
        PithInclusion PithInclusion = new PithInclusion();
        

        public MainWindow()
        {
            InitializeComponent();
            DataContext = PithInclusion;
            KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {   
                //Start the pith labeling mode
                case Key.H: 
                    PithLabelingON = true;
                    //Update spline instantly if restarting the pith labeling on existing spline
                    if (PithInclusion.ControlPoints.Count() >= 1) 
                    {
                        var AtClickPoint = Mouse.GetPosition(Application.Current.MainWindow);
                        PithInclusion.ControlPoints.Add(AtClickPoint);
                        PithInclusion.UpdateSpline();
                    }
                    break;

                case Key.Delete:
                    //if there is no valid user selected spline control point index, just ignore
                    if (UserSelectCtrlPointIdx == -1) return;
                    //Delete the user selected pith control point
                    PithInclusion.ControlPoints.RemoveAt(UserSelectCtrlPointIdx);
                    PithInclusion.UpdateSpline();
                    UserSelectCtrlPointIdx = -1;//safe gaurd from user pressing repeated DELETE key
                    PithInclusion.UserSplineControlPoints -= 1;//remove the user spline control points
                    break;

                case Key.Escape:
                    if (PithInclusion.ControlPoints.Count()>=1) ResetSelectSplineEllips();
                    if (PithLabelingON) //If pith labeling is ON turn it OFF
                    {
                        UserSelectCtrlPointIdx = -1;
                        UserSelectCtrlPointEllipse = null;
                        PithLabelingON = false;
                        PithInclusion.UpdateSpline();
                    }
                    break;

                case Key.Z: //enabling Ctrl+Z while pith labeling to undo the last added point
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        if (!PithLabelingON) break;
                        PithInclusion.ControlPoints.RemoveAt(PithInclusion.ControlPoints.Count() - 1);
                        PithInclusion.UserSplineControlPoints -= 1;
                        PithInclusion.UpdateSpline();
                    }
                    break;
            }
        }
        
        private void MainWindow_LeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if there is still user selected control point visible on screen, reset it.
            if (UserSelectCtrlPointIdx >= 0) ResetSelectSplineEllips();

            //if user press left Control key and left mouse key, a user control point should be added between two closest control point on the spline.
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && PithInclusion.ControlPoints.Count() >= 2) 
            {
                //getting the current mouse position and using that, getting indexes of nearest neighbor control point to that mouse location
                var AtClickPoint = Mouse.GetPosition(Application.Current.MainWindow);
                var NeighborsAtClick = PithInclusion.GetNearestControlPointsIdx(AtClickPoint);

                //Add point at that index, moving rest points one spot to the right in the control points list
                PithInclusion.ControlPoints.Insert(NeighborsAtClick, AtClickPoint);
                PithInclusion.UserSplineControlPoints += 1;

                //Only update spline if its valid - we will need at least 2 control points 
                if (PithInclusion.ControlPoints.Count() >= 2) PithInclusion.UpdateSpline(); //this update is for control point

            }
            
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && PithLabelingON)
            {
                //adding current point to the control points
                var AtClickPoint = Mouse.GetPosition(Application.Current.MainWindow);
                PithInclusion.UserSplineControlPoints += 1;
                PithInclusion.ControlPoints.Add(AtClickPoint);

                //Only update spline if its valid - we will need at least 2 control points 
                if (PithInclusion.ControlPoints.Count() >= 2 ) PithInclusion.UpdateSpline(); //this update is for control point
            }
        }
        
        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (PithInclusion.ControlPoints.Count() == 0) return;

            /******* Spline: P1--------P2--------P3-------P4..P5..PN-----MouseArrow
             ** if pith labeling is ON, we need to show user the latest predicted path with mouse hovering constantly. 
             ** To update, we temporary store mouse coordinate with control points, update the spline and afterwards, remove that temporary point from control points
             ** We need to keep doing this while the mouse is moving 
             ** */

            if (PithLabelingON)
            {
                //adding current mouse position to control point
                var CurrentMouseCoord = Mouse.GetPosition(Application.Current.MainWindow);
                PithInclusion.ControlPoints.Add(CurrentMouseCoord);

                //updating the spline temporarily
                PithInclusion.UpdateSpline();

                //removing the extra temporary points, sometimes multiple points are generated so make sure to clean them all
                while (PithInclusion.ControlPoints.Count() > PithInclusion.UserSplineControlPoints)
                    PithInclusion.ControlPoints.RemoveAt(PithInclusion.ControlPoints.Count() - 1);
            }
            else if ( PithCPointMoveInProg && UserSelectCtrlPointIdx >= 0) //if pith control point move is in progress
            {
                //Replace user selected Control point with current mouse coordinates
                var CurrentMouseCoord = Mouse.GetPosition(Application.Current.MainWindow);
                PithInclusion.ControlPoints[UserSelectCtrlPointIdx] = CurrentMouseCoord;
                PithInclusion.UpdateSpline();
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //no point is slected anymore
            PithCPointMoveInProg = false;
        }

        //When mouse appears on the ellipse drawn for the control points, it should have hover animation
        private void SplinePoints_IsMouseDirectlyOverChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var lEllipse = sender as Ellipse; //get the current ellipse
            //if mouse is directly over, change the diameter
            if (lEllipse.IsMouseDirectlyOver)
            {
                lEllipse.Width = 15;
                lEllipse.Height = 15;
                lEllipse.Margin = new Thickness(-7.5,-7.5,7.5,7.5);
            }
            else 
            //if user in progress of adjusting selected spline control point, return
            //if user has already selected a spline control point, intension might be to move/delete, return
            //If all test passed, and if mouse is not directly over anymore, reset ellipse to default
            //and reset the user selected control point index to -1
            {
                if (UserSelectCtrlPointEllipse == lEllipse) return;
                lEllipse.Width = 10;
                lEllipse.Height = 10;
                lEllipse.Margin = new Thickness(-5, -5, 5, 5);
                lEllipse.Fill = Brushes.Blue;
                lEllipse.Stroke = null;
            }
        }

        //When mouse is clicked over the ellipse, first get the index of selected point.
        //Turn on the move mode since user may have selected that point to move and adjust the spline
        //Also, set the visual of the selected point to 'selected point'
        private void SplinePoints_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if there is already one ellipse selected and user slected control point index is not -1
            if (UserSelectCtrlPointIdx >= 0) ResetSelectSplineEllips();

            //Get the index of the user selected control point
            var AtClickPoint = Mouse.GetPosition(Application.Current.MainWindow); //get the current mouse position
            UserSelectCtrlPointIdx = PithInclusion.GetNearestControlPointsIdx(AtClickPoint, true); //find out index of control point, where mouse was clicked

            
            //Turn On the move mode
            PithCPointMoveInProg = true;

            //setting ellipse to selected point visual
            UserSelectCtrlPointEllipse = sender as Ellipse;
            UserSelectCtrlPointEllipse.Fill = Brushes.Red;
            UserSelectCtrlPointEllipse.Stroke = Brushes.Black;

            //stop event from bubbling up
            e.Handled = true;
        }

        //When mouse is moved away from the previously selected point, and clicked somewhere else except on spline control points,
        //we need to reset previously selected ellipse for that control point
        private void ResetSelectSplineEllips()
        {
            if (UserSelectCtrlPointEllipse == null) return;
            
            UserSelectCtrlPointEllipse.Fill = Brushes.Blue;
            UserSelectCtrlPointEllipse.Stroke = null;
            UserSelectCtrlPointEllipse.Width = 10;
            UserSelectCtrlPointEllipse.Height = 10;
            UserSelectCtrlPointEllipse.Margin = new Thickness(-5, -5, 5, 5);

            //reset selected idx and ellipse
            UserSelectCtrlPointIdx = -1;
            UserSelectCtrlPointEllipse = null;
        }
    }
}
