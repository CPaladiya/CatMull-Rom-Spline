using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;
using System.Numerics;

namespace PithExp
{
    /* This class stores control points, spline points generated through control points.
    ** Includes Routines to generate or update existing CatMull-Rom spline and
    ** Utility functions to calculate distance between two points, points and line, and angles formed by three points not on a single line
    *********/
    public class PithInclusion
    {
        #region Variables

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ObservableCollection<Point> m_ControlPoints = new ObservableCollection<Point>();
        public ObservableCollection<Point> ControlPoints // all the control points clicked by user
        {
            get { return m_ControlPoints; }
            set { m_ControlPoints = value; OnPropertyChanged("ControlPoints"); }
        }

        public ObservableCollection<Point> m_SplinePoints = new ObservableCollection<Point>();
        public ObservableCollection<Point> SplinePoints // all the points generated through spline interpolation
        {
            get { return m_SplinePoints; }
            set { m_SplinePoints = value; OnPropertyChanged("SplinePoints"); }
        }

        public int UserSplineControlPoints = 0; //no of spline points added by user, temporary control points added to show live preview update is ignored.

        public double m_SplineRes = 20;// Unit distance between two consecutive spline points which are calucated using two control points.
        public double SplineRes
        {
            get { return m_SplineRes; }
            set { m_SplineRes = value; OnPropertyChanged("SplineRes"); UpdateSpline(); }
        }
        public double Start_t = 0.01; //starting t value - t can be understood from GetCatMullSplinePoint() function
        public double End_t = 0.99; //ending t value 

        #endregion Variables

        #region Spline Routines

        /* The function to generate Catmull–Rom spline,
         ** The equation used here can be found at wikipedia page https://en.wikipedia.org/wiki/Cubic_Hermite_spline (Catmull–Rom spline)
         *********
         ** Using four points - P0, P1, P2 and P3, Spline will only be generated between point P1 and P2
         ** P1 and P2 are 'on-curve' control points. P0 and P3 are 'influence' control points - that affects the spline generation between P1/P2
         *********
         ** the interpolation parameter is between 0 and 1
         **            |<-->| = t
         **     P0-----P1-------P2-----P3
         **            |    ^   |
         **            t=0  t  t=1
         ** Here t is distance between P1 and P2 - in terms of fraction. Total distance between them is 1. 
         ** for example, at 0 you will be at P1 and 1, you will be at P2. Anything in between 0 and 1, will bring you between P1 and P2.
         **
         ** f(t) = At³ + Bt² + Ct + D - Cubic equation in interest!
         **
         ** Given the control points P0, P1, P2, and P3, and the value t, 
         ** the location of the point can be calculated as (assuming uniform spacing of control points):
         ** f(t) = 0.5 * ( -------
         **                  A=>  (-P0 + 3*P1 - 3*P2 + P3)*ttt 
         **                  B=>  (2*P0 - 5*P1 + 4*P2 - P3)*tt +
         **                  C=>  (-P0 + P2)*t +
         **                  D=>  (2*P1) + 
         **                   -------)
         ** Above equation is used to get derived x and derived y for a given t value
         **********/
        private Point GetCatMullSplinePoint(Point P0, Point P1, Point P2, Point P3, double t)
        {
            //getting all the x,y positions of the points for the given value of t
            double X0 = P0.X, X1 = P1.X, X2 = P2.X, X3 = P3.X;
            var A_x = (-X0 + 3 * X1 - 3 * X2 + X3) ;
            var B_x = (2 * X0 - 5 * X1 + 4 * X2 - X3);
            var C_x = (-X0 + X2);
            var D_x = 2 * X1;

            double Y0 = P0.Y, Y1 = P1.Y, Y2 = P2.Y, Y3 = P3.Y;
            var A_y = (-Y0 + 3 * Y1 - 3 * Y2 + Y3);
            var B_y = (2 * Y0 - 5 * Y1 + 4 * Y2 - Y3);
            var C_y = (-Y0 + Y2);
            var D_y = 2 * Y1;

            var ttt = t * t * t;
            var tt = t * t;
            
            //Final x,y location of the point at t,
            Point lSplinePoint = new Point();
            lSplinePoint.X = 0.5 * (A_x * ttt + B_x * tt + C_x * t + D_x);
            lSplinePoint.Y = 0.5 * (A_y * ttt + B_y * tt + C_y * t + D_y);

            return lSplinePoint;
        }

        #endregion Spline Routines

        #region Utilities

        /*Input : Current mouse position, onlyMostNearestPoint Flag
        **Returns : if onlyMostNearestPoint = true : Index of nearest control point to the mouse
        **Returns : if onlyMostNearestPoint = false : Index of control point where mouse point should be added to add new control points in between existing control points
        ********/
        public int GetNearestControlPointsIdx(Point MousePosition, bool onlyMostNearestPoint = false)
        {
            if (ControlPoints.Count() == 0) return -1;//special case no points and function is triggered

            //if we are only interested in index of nearest point for hovering animation purpose
            //OR we only have two points present currently
            if (onlyMostNearestPoint)
            {
                var DistanceList = new List<double>();
                for (int i = 0; i < ControlPoints.Count(); i++)
                    DistanceList.Add(DistanceBetWPoints(MousePosition, ControlPoints[i]));
                return DistanceList.IndexOf(DistanceList.Min());
            }
            else if (ControlPoints.Count() == 2) return 1; //if we only have 2 control point for insertion of new control point index will be 1.

            /*if we are interested in finding index of nearest control point from mouse point to add new control point to the spline
            **And we have >= 3 control points, let's find APlusC for all the lines made by two consecutive control points and mouse points
            **Whichever line has least A+C is the closest to mouse point
            **         MousePoint
            **        / | \
            **       /  |  \
            **      /   |   \
            **     /A__ | __C\B________  
            **   P0           P1  
            ***********/
            var APlusBAngleList = new List<double>();
            for (int i = 0; i < ControlPoints.Count() - 1; i++)
                APlusBAngleList.Add(APlusCAngle(ControlPoints[i], ControlPoints[i + 1], MousePosition));

            int MinAPlusBAngleIdx = APlusBAngleList.IndexOf(APlusBAngleList.Min());
            return MinAPlusBAngleIdx + 1;
        }

        /*Direct distance beween indivdual point and a mouse point
        **       P1
        **      /        
        **     /Direct distance
        **    /          
        **  P0 
        **********/
        public double DistanceBetWPoints(Point P0, Point P1)
        {
            return Point.Subtract(P0,P1).Length;
        }

        /*straight distance between line and a point. Line is formed by point P1 and P0. :https://en.wikipedia.org/wiki/Distance_from_a_point_to_a_line
        **              Mouse Point
        **              |
        **              |Stright distance between line formed by points (P0 & P1) and mouse point
        **              |
        **  P0--------LINE---------P1 
        **********/
        public double DistBetwnLineAndPoint(Point P0, Point P1, Point MousePoint)
        {
            var Denominator = DistanceBetWPoints(P0, P1);
            var Numerator = Math.Abs(( (P1.X - P0.X) * (P0.Y - MousePoint.Y) ) - ( (P0.X - MousePoint.X) * (P1.Y - P0.Y)) );

            return Numerator / Denominator;
        }

        /*Sum of angle A+B formed by line (made with points P0 & P1) and top corner of triangle made by mouse points (any third point)
        **         MousePoint
        **        / | \
        **       /  |  \
        **   p0d/   |d0 \p1d
        **     /A__ | __C\B________  
        **   P0    p01    P1 
        **********/
        public double APlusCAngle(Point P0, Point P1, Point MousePoint)
        {
            //three different vectors
            Vector2 p0d = new Vector2((float)Point.Subtract(P0, MousePoint).X, (float)Point.Subtract(P0, MousePoint).Y);
            Vector2 p1d = new Vector2((float)Point.Subtract(P1, MousePoint).X, (float)Point.Subtract(P1, MousePoint).Y);
            Vector2 p01 = new Vector2((float)Point.Subtract(P0, P1).X, (float)Point.Subtract(P0, P1).Y);

            var ThetaA = Math.Acos( Vector2.Dot(p0d, p01) / (p0d.Length() * p01.Length()) );
            var ThetaB = Math.Acos( Vector2.Dot(p01, p1d) / (p1d.Length() * p01.Length()) );
            var ThetaC = Math.PI - ThetaB;

            return Math.Abs(ThetaA) + Math.Abs(ThetaC);
        }

        #endregion Utilities

        #region Spline Generation

        //Calculates increment based on user's requirement of spline points per unit distance
        private double CalculateIncrement(Point P1, Point P2)
        {
            double Distance = Point.Subtract(P2, P1).Length; //distance between two points
            double NumberOfPoints = Distance / SplineRes;
            return (1.0 / (NumberOfPoints<6? 6: NumberOfPoints));
        }

        /* Based on points available to generate spline,
         **
         ** 0/1 - return
         **
         ** 2 - xp0---F---xp1 
         **     > For first line segment    (F): xp0 influence, xp0 control, xp1 control, xp1 influence
         **
         ** 3 - xP0---F---xP1----L----xP2
         **     > For first line segment    (F): xp0 influence, xp0 control, xp1 control, xp2 influence
         **     > For last section          (L): xp0 influence, xp1 control, xp2 control, xp2 influence
         **
         ** 4 or more - example - xP0---F----xP1----M----xP2----M-----xP3----L-----xP4
         **     > For first line segment    (F): xp0 influence, xp0 control, xp1 control, xp2 influence
         **     > For 2nd to 2nd-last segmt (M): xpn-1 influence, xpn control, xpn+1 control, xpn+2 influence
         **     > For last section          (L): xp2 influence, xp3 control, xp4 control, xp4 influence
         ************/
        public void UpdateSpline()
        {
            double Increment;
            SplinePoints.Clear();
            switch (ControlPoints.Count()) //number of points available to generate spline
            {
                case 0:
                case 1:
                    return;

                case 2: //When user selected one point and second generated due to hover of the mouse

                    // xp0---F---Mouse : first and only segment of the spline
                    Increment = CalculateIncrement(ControlPoints[0], ControlPoints[1]);
                    for (double j = Start_t; j < End_t; j += Increment)
                        SplinePoints.Add(GetCatMullSplinePoint(ControlPoints[0], ControlPoints[0], ControlPoints[1], ControlPoints[1], j));
                    break;

                case 3: // xP0---F---xP1----L----Mouse : When user selected two points and third generated due to hover of the mouse

                    // xP0---F---xP1: first segment of the spline
                    Increment = CalculateIncrement(ControlPoints[0], ControlPoints[1]);
                    for (double j = Start_t; j < End_t; j += Increment)
                        SplinePoints.Add(GetCatMullSplinePoint(ControlPoints[0], ControlPoints[0], ControlPoints[1], ControlPoints[2], j));

                    // xP1----L----Mouse: secon segment of the spline
                    Increment = CalculateIncrement(ControlPoints[1], ControlPoints[2]);
                    for (double j = Start_t; j < End_t; j += Increment)
                        SplinePoints.Add(GetCatMullSplinePoint(ControlPoints[0], ControlPoints[1], ControlPoints[2], ControlPoints[2], j));
                    break;

                default: //xP0---F----xP1----M----xP2----M-----xP3----L-----Mouse
                         //When user has selected at least three points and last one generated due to hover of the mouse

                    // xP0---F----xP1: first segment of the spline
                    Increment = CalculateIncrement(ControlPoints[0], ControlPoints[1]);
                    for (double j = Start_t; j < End_t; j += Increment)
                        SplinePoints.Add(GetCatMullSplinePoint(ControlPoints[0], ControlPoints[0], ControlPoints[1], ControlPoints[2], j));

                    // xP1----M----xP2----M-----xP3: second to second last segment of the spline
                    for (int k = 0; k < ControlPoints.Count() - 3; k++)
                    {
                        Increment = CalculateIncrement(ControlPoints[k + 1], ControlPoints[k + 2]);
                        for (double l = Start_t; l < End_t; l += Increment)
                            SplinePoints.Add(GetCatMullSplinePoint(ControlPoints[k], ControlPoints[k + 1], ControlPoints[k + 2], ControlPoints[k + 3], l));
                    }

                    // xP3----L-----Mouse: last segment of the spline
                    var idx = ControlPoints.Count() - 1;
                    Increment = CalculateIncrement(ControlPoints[idx - 1], ControlPoints[idx]);
                    for (double j = Start_t; j < End_t; j += Increment)
                        SplinePoints.Add(GetCatMullSplinePoint(ControlPoints[idx - 2], ControlPoints[idx - 1], ControlPoints[idx], ControlPoints[idx], j));
                    break;
            }
        }
        #endregion Spline Generation
    };
}



