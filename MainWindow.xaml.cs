using HalconDotNet;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Rectangle rectangle = new Rectangle();
        Line line = new Line();
        Ellipse ellipse = new Ellipse();
        
        Shape Shape;
        bool isMouseDown = false;

        public Point start_point, end_point;
        private double _k = 1;
        private double _tx = 0;
        private double _ty = 0;
        double r1, r2, c1, c2;
        
        EShape ShapeType;

        List<HDrawingObject> DrawingObjects = new List<HDrawingObject>();
        
        HWindow HWindow;

        public MainWindow()
        {
            InitializeComponent();
            Initial();
            DataContext = this;
        }

        public void Initial()
        {
            rectangle.StrokeThickness = 0;
            line.StrokeThickness = 0;
            ellipse.StrokeThickness = 0;
            cvs.Children.Add(rectangle);
            cvs.Children.Add(line);
            cvs.Children.Add(ellipse);
        }
        /// <summary>
        /// halcon窗体初始化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void smartControl_HInitWindow(object sender, EventArgs e)
        {
            HWindow = smartControl.HalconWindow;

            //参考文档网址：https://blog.csdn.net/sinat_21001719/article/details/128647619
            #region 比例与偏移计算
            var dpd = DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF));
            dpd.AddValueChanged(smartControl, (o, es) =>
            {
                var imgPart = smartControl.HImagePart;
                _k = imgPart.Height / smartControl.ActualHeight;
                _tx = imgPart.X;
                _ty = imgPart.Y;
            });

            #endregion
        }

        /// <summary>
        /// 鼠标左键按下事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cvs_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            start_point = e.GetPosition(cvs);
            isMouseDown = true;
            Shape.StrokeThickness = 1;
            Shape.Stroke = Brushes.Red;
        }
        /// <summary>
        /// 鼠标右键按下事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cvs_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            cvs.Visibility = Visibility.Collapsed;
        }
        /// <summary>
        /// 鼠标左键抬起事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cvs_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isMouseDown = false;
            DumpRC();
            end_point = e.GetPosition(cvs);

            HDrawingObject obj =  DrawShape();
           if (obj != null)
            {
                obj.SetDrawingObjectParams(new HTuple("color"), new HTuple("red"));
                HWindow.AttachDrawingObjectToWindow(obj);
            }
            ClearDrawData();

            //HHomMat2D homMat2D = new HHomMat2D();
            //homMat2D.HomMat2dTranslate(_tx, _ty);
            //homMat2D.HomMat2dScale(1, 1, _k, _k);
            //homMat2D.AffineTransRegion(obj, "nearest_neighbor");
        }
        /// <summary>
        /// 鼠标移动事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cvs_MouseMove(object sender, MouseEventArgs e)
        {
            end_point = e.GetPosition(cvs);
            if (isMouseDown)
            {
                double x_max = Math.Max(start_point.X, end_point.X);
                double x_min = Math.Min(start_point.X, end_point.X);
                double y_max = Math.Max(start_point.Y, end_point.Y);
                double y_min = Math.Min(start_point.Y, end_point.Y);
                switch (ShapeType)
                {
                    case EShape.Rectangle:
                        Shape.Margin = new Thickness(x_min, y_min, 0, 0);
                        Shape.Width = x_max - x_min;
                        Shape.Height = y_max - y_min;
                        break;
                    case EShape.Ellipse:
                        double radius = Math.Sqrt((x_max - x_min) * (x_max - x_min) + (y_max - y_min) * (y_max - y_min));
                        Shape.Margin = new Thickness(x_min - radius, y_min - radius, 0, 0);
                        Shape.Width = radius * 2;
                        Shape.Height = radius * 2;
                        break;
                    case EShape.Line:
                        ((Line)Shape).X1 = start_point.X;
                        ((Line)Shape).Y1 = start_point.Y;
                        ((Line)Shape).X2 = end_point.X;
                        ((Line)Shape).Y2 = end_point.Y;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 绘制图形
        /// </summary>
        /// <returns></returns>
        private HDrawingObject DrawShape()
        {
            return ShapeType switch
            {
                EShape.Rectangle => DrawRectangle(),
                EShape.Ellipse => DrawEllipse(),
                EShape.Line => DrawLine(),
                _ => DrawEllipse()
            };
        }
        /// <summary>
        /// 绘制矩形
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawRectangle()
        {
            r1 = _k * Math.Min(start_point.Y, end_point.Y) + _ty;
            r2 = _k * Math.Max(start_point.Y, end_point.Y) + _ty;
            c1 = _k * Math.Min(start_point.X, end_point.X) + _tx;
            c2 = _k * Math.Max(start_point.X, end_point.X) + _tx;
            if (Math.Abs(r1 - r2) < 1 || Math.Abs(c1 - c2) < 1)
            {
                return null;
            }
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE1, r1, c1, r2, c2);
            DrawingObjects.Add(obj);
            return obj;
        }
        /// <summary>
        /// 绘制圆形
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawEllipse()
        {
            r1 = _k * Math.Min(start_point.Y, end_point.Y) + _ty;
            r2 = _k * Math.Max(start_point.Y, end_point.Y) + _ty;
            c1 = _k * Math.Min(start_point.X, end_point.X) + _tx;
            c2 = _k * Math.Max(start_point.X, end_point.X) + _tx;
            double center_r = start_point.Y * _k + _ty;
            double center_c = start_point.X * _k + _tx;
            double r = Math.Sqrt((r1 - r2) * (r1 - r2) + (c1 - c2) * (c1 - c2));
            if (r < 1)
            {
                return null;
            }
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE, center_r, center_c, r);
            DrawingObjects.Add(obj);
            return obj;
        }
        /// <summary>
        /// 绘制线
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawLine()
        {
            r1 = _k * start_point.Y + _ty;
            r2 = _k * end_point.Y + _ty;
            c1 = _k * start_point.X + _tx;
            c2 = _k * end_point.X + _tx;
            if (Math.Abs(r1 - r2) < 1 && Math.Abs(c1 - c2) < 1)
            {
                return null;
            }
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.LINE, r1, c1, r2, c2);
            DrawingObjects.Add(obj);
            return obj;
        }

        public void DumpRC()
        {
            Debug.WriteLine($"({r1},{c1}), ({r2},{c2})");
        }

        private void Rectangle_Button_Click(object sender, RoutedEventArgs e)
        {
            cvs.Visibility = Visibility.Visible;
            ShapeType = EShape.Rectangle;
            Shape = rectangle;
        }
        private void Ellipse_Button_Click(object sender, RoutedEventArgs e)
        {
            cvs.Visibility = Visibility.Visible;
            ShapeType = EShape.Ellipse;
            Shape = ellipse;
        }
        private void Line_Button_Click(object sender, RoutedEventArgs e)
        {
            cvs.Visibility = Visibility.Visible;
            ShapeType = EShape.Line;
            Shape = line;
        }

        /// <summary>
        /// 清除ROI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Clear_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.Dispose();
            }
            DrawingObjects.Clear();
        }
        /// <summary>
        /// 清除绘制过程中的数据
        /// </summary>
        private void ClearDrawData()
        {
            if (ShapeType == EShape.Line)
            {
                ((Line)Shape).X1 = 0;
                ((Line)Shape).Y1 = 0;
                ((Line)Shape).X2 = 0;
                ((Line)Shape).Y2 = 0;
            }
            Shape.Width = 0;
            Shape.Height = 0;
            Shape.StrokeThickness = 0;
            cvs.Visibility = Visibility.Collapsed;
        }
    }

    public enum EShape
    {
        Rectangle,
        Ellipse,
        Line,
    }
}