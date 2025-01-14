﻿using HalconDotNet;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
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
using static HalconDotNet.HDrawingObject;

namespace WpfApp1
{
    /// <summary>
    /// ROICanvas.xaml 的交互逻辑
    /// </summary>
    public partial class ROICanvas : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        EShape ShapeType;
        Shape Shape;
        EROIMode Mode; 

        //roi集合
        ObservableCollection<HDrawingObject> DrawingObjects = new ObservableCollection<HDrawingObject>();
        HDrawingObject SelectedHDrawingObject;
        //roi数据集合
        public ObservableCollection<ROIData> ROIDatas { get; set; } = new ObservableCollection<ROIData>();

        string SelectedROIType;

        public HImage DisplayImage { get; set; }

        HWindow HWindow;

        bool isMouseDown = false;

        private double _k = 1;
        private double _tx = 0;
        private double _ty = 0;

        public Point start_point, end_point;
        double r1, r2, c1, c2;
        double angle = 0;

        private BitmapImage _selectedModeImg = new BitmapImage(new Uri("/Resources/union.png", UriKind.Relative));
        public BitmapImage SelectedModeImg
        {
            get => _selectedModeImg;
            set
            {
                if (_selectedModeImg != value)
                {
                    _selectedModeImg = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeImg)));
                }
            }
        }

        public ROICanvas()
        {
            InitializeComponent();
            DisplayImage = new HImage(@"C:\Users\Lenovo\Downloads\123.png");
            DataContext = this;
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
        private void Helper_Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            start_point = e.GetPosition(Helper_Canvas);
            isMouseDown = true;
        }
        /// <summary>
        /// 鼠标右键按下事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Helper_Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            isMouseDown = false;
            ClearDrawData();
        }
        /// <summary>
        /// 鼠标左键抬起事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Helper_Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isMouseDown = false;
            end_point = e.GetPosition(Helper_Canvas);

            HDrawingObject obj = DrawShape();
            if (obj != null)
            {
                obj.SetDrawingObjectParams(new HTuple("color"), new HTuple("red"));
                HWindow.AttachDrawingObjectToWindow(obj);
                DrawingObjects.Add(obj);
                obj.OnDrag(HDrawingObjectCallbackClass);
                obj.OnResize(HDrawingObjectCallbackClass);

                obj.OnAttach(HDrawingObjectCallbackClass);
                obj.OnSelect(HDrawingObjectCallbackClass);
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
        private void Helper_Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            end_point = e.GetPosition(Helper_Canvas);
            //tb.Text = $"x1:{start_point.X},y1:{start_point.Y},x2:{end_point.X},y2:{end_point.Y}";
            if (isMouseDown)
            {
                GetCornerPointPos(out double x_max, out double x_min, out double y_max, out double y_min);
                switch (ShapeType)
                {
                    case EShape.Rectangle1:
                        Shape.Margin = new Thickness(x_min, y_min, 0, 0);
                        Shape.Width = x_max - x_min;
                        Shape.Height = y_max - y_min;
                        break;
                    case EShape.Rectangle2:
                        double width = Math.Sqrt(Math.Pow(x_max - x_min, 2) + Math.Pow(y_max - y_min, 2));
                        double height = 20; // 固定高度，可以根据需要修改
                        Shape.Margin = new Thickness(start_point.X - width, start_point.Y - height / 2, 0, 0);
                        Shape.Width = width * 2;
                        Shape.Height = height;

                        Shape.RenderTransformOrigin = new Point(0.5, 0.5);
                        Shape.RenderTransform = new RotateTransform(angle);
                        break;
                    case EShape.Circle:
                        double circle_radius = Math.Sqrt((x_max - x_min) * (x_max - x_min) + (y_max - y_min) * (y_max - y_min));
                        //起点即是圆心
                        Shape.Margin = new Thickness(start_point.X - circle_radius, start_point.Y - circle_radius, 0, 0);
                        Shape.Width = circle_radius * 2;
                        Shape.Height = circle_radius * 2;
                        break;
                    case EShape.Ellipse:
                        double radius_l = Math.Sqrt(Math.Pow(end_point.X - start_point.X, 2) + Math.Pow(end_point.Y - start_point.Y, 2));
                        double radius_s = 20; // 固定高度，可以根据需要修改
                        Shape.Margin = new Thickness(start_point.X - radius_l, start_point.Y - radius_s / 2, 0, 0);
                        Shape.Width = radius_l * 2;
                        Shape.Height = radius_s;
                        Shape.RenderTransformOrigin = new Point(0.5, 0.5);
                        Shape.RenderTransform = new RotateTransform(angle);
                        break;
                    case EShape.Line:
                        ((Line)Shape).X1 = x_min;
                        ((Line)Shape).Y1 = y_min;
                        ((Line)Shape).X2 = x_max;
                        ((Line)Shape).Y2 = y_max;
                        break;
                    default:
                        break;
                }
            }
        }

        public void HDrawingObjectCallbackClass(HDrawingObject drawobj, HWindow window, string type1)
        {

            if (SelectedHDrawingObject == null || SelectedHDrawingObject.TupleIsValidHandle().I != 1 || drawobj.ID != SelectedHDrawingObject.ID)
            {
                SelectedHDrawingObject = drawobj;
            }


            var type = drawobj.GetDrawingObjectParams("type");
            if (type == "circle")//圆形
            {
                if (ROIDatas.Count == 0 || type != SelectedROIType)
                {
                    SelectedROIType = type;
                    ROIDatas.Clear();
                    ROIDatas.Add(new ROIData("Row"));
                    ROIDatas.Add(new ROIData("Column"));
                    ROIDatas.Add(new ROIData("Radius"));
                }
                ROIDatas[0].Value = drawobj.GetDrawingObjectParams("row").D;
                ROIDatas[1].Value = drawobj.GetDrawingObjectParams("column").D;
                ROIDatas[2].Value = drawobj.GetDrawingObjectParams("radius").D;
            }
            else if (type == "ellipse")//椭圆
            {
                if (ROIDatas.Count == 0 || type != SelectedROIType)
                {
                    SelectedROIType = type;
                    ROIDatas.Clear();
                    ROIDatas.Add(new ROIData("Row"));
                    ROIDatas.Add(new ROIData("Column"));
                    ROIDatas.Add(new ROIData("Radius1"));
                    ROIDatas.Add(new ROIData("Radius2"));
                    ROIDatas.Add(new ROIData("Phi"));
                }
                ROIDatas[0].Value = drawobj.GetDrawingObjectParams("row").D;
                ROIDatas[1].Value = drawobj.GetDrawingObjectParams("column").D;
                ROIDatas[2].Value = drawobj.GetDrawingObjectParams("radius1").D;
                ROIDatas[3].Value = drawobj.GetDrawingObjectParams("radius2").D;
                ROIDatas[4].Value = drawobj.GetDrawingObjectParams("phi").D;
            }
            else if (type == "rectangle2")//仿距
            {
                if (ROIDatas.Count == 0 || type != SelectedROIType)
                {
                    SelectedROIType = type;
                    ROIDatas.Clear();
                    ROIDatas.Add(new ROIData("Row"));
                    ROIDatas.Add(new ROIData("Column"));
                    ROIDatas.Add(new ROIData("Length1"));
                    ROIDatas.Add(new ROIData("Length2"));
                    ROIDatas.Add(new ROIData("Phi"));
                }
                ROIDatas[0].Value = drawobj.GetDrawingObjectParams("row").D;
                ROIDatas[1].Value = drawobj.GetDrawingObjectParams("column").D;
                ROIDatas[2].Value = drawobj.GetDrawingObjectParams("length1").D;
                ROIDatas[3].Value = drawobj.GetDrawingObjectParams("length2").D;
                ROIDatas[4].Value = drawobj.GetDrawingObjectParams("phi").D;
            }
            else if (ROIDatas.Count == 0 || type == "rectangle1")//矩形
            {
                if (ROIDatas.Count == 0 || type != SelectedROIType)
                {
                    SelectedROIType = type;
                    ROIDatas.Clear();
                    ROIDatas.Add(new ROIData("Row1"));
                    ROIDatas.Add(new ROIData("Column1"));
                    ROIDatas.Add(new ROIData("Row2"));
                    ROIDatas.Add(new ROIData("Column2"));
                }
                ROIDatas[0].Value = drawobj.GetDrawingObjectParams("row1").D;
                ROIDatas[1].Value = drawobj.GetDrawingObjectParams("column1").D;
                ROIDatas[2].Value = drawobj.GetDrawingObjectParams("row2").D;
                ROIDatas[3].Value = drawobj.GetDrawingObjectParams("column2").D;
            }
            else if (ROIDatas.Count == 0 || type == "line")//直线
            {
                if (type != SelectedROIType)
                {
                    SelectedROIType = type;
                    ROIDatas.Clear();
                    ROIDatas.Add(new ROIData("Row1"));
                    ROIDatas.Add(new ROIData("Column1"));
                    ROIDatas.Add(new ROIData("Row2"));
                    ROIDatas.Add(new ROIData("Column2"));
                }
                ROIDatas[0].Value = drawobj.GetDrawingObjectParams("row1").D;
                ROIDatas[1].Value = drawobj.GetDrawingObjectParams("column1").D;
                ROIDatas[2].Value = drawobj.GetDrawingObjectParams("row2").D;
                ROIDatas[3].Value = drawobj.GetDrawingObjectParams("column2").D;

            }
        }

        /// <summary>
        /// 获取绘制图案的左上角与右下角
        /// </summary>
        /// <param name="x_max">右下角x</param>
        /// <param name="x_min">左上角x</param>
        /// <param name="y_max">右下角y</param>
        /// <param name="y_min">左上角y</param>
        private void GetCornerPointPos(out double x_max, out double x_min, out double y_max, out double y_min)
        {
            //线段自带方向，鼠标按下的点就是起点
            if (ShapeType == EShape.Line)
            {
                x_min = start_point.X;
                y_min = start_point.Y;
                x_max = end_point.X;
                y_max = end_point.Y;
            }
            else
            {
                //计算绘制的图案左上角与右下角,设置margin,用于摆放图像位置
                x_max = Math.Max(start_point.X, end_point.X);
                x_min = Math.Min(start_point.X, end_point.X);
                y_max = Math.Max(start_point.Y, end_point.Y);
                y_min = Math.Min(start_point.Y, end_point.Y);
                //这两个图形有方向，需要计算角度
                if (ShapeType == EShape.Rectangle2 || ShapeType == EShape.Ellipse)
                {
                    angle = Math.Atan2(end_point.Y - start_point.Y, end_point.X - start_point.X) * (180 / Math.PI);
                }
            }
        }
        /// <summary>
        /// 转换画布坐标关系到smart窗口上
        /// </summary>
        private void ConvertCoordinate()
        {
            GetCornerPointPos(out double x_max, out double x_min, out double y_max, out double y_min);
            ConvertPoint(x_min, y_min, out Point sp);
            ConvertPoint(x_max, y_max, out Point ep);
            //图像像素row1,row2,column1,column2
            r1 = sp.Y;
            c1 = sp.X;
            r2 = ep.Y;
            c2 = ep.X;
        }
        /// <summary>
        /// 转换点坐标
        /// </summary>
        /// <param name="px">画布x</param>
        /// <param name="py">画布y</param>
        /// <param name="new_point">图像像素点</param>
        private void ConvertPoint(double px, double py, out Point new_point)
        {
            new_point = new Point()
            {
                X = _k * px + _tx,
                Y = _k * py + _ty
            };
        }

        /// <summary>
        /// 绘制图形
        /// </summary>
        /// <returns></returns>
        private HDrawingObject DrawShape()
        {
            ConvertCoordinate();
            if (!IsValidShape())
            {
                return null;
            }
            return ShapeType switch
            {
                EShape.Rectangle1 => DrawRectangle1(),
                EShape.Rectangle2 => DrawRectangle2(),
                EShape.Circle => DrawCircle(),
                EShape.Ellipse => DrawEllipse(),
                _ => DrawLine()
            };
        }
        /// <summary>
        /// 绘制矩形
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawRectangle1()
        {
            double row1 = r1;
            double row2 = r2;
            double column1 = c1;
            double column2 = c2;
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE1, row1, column1, row2, column2);
            return obj;
        }
        /// <summary>
        /// 绘制旋转矩形
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawRectangle2()
        {
            ConvertPoint(start_point.X, start_point.Y, out Point point);
            double row = point.Y;
            double column = point.X;
            double phi = angle * Math.PI / 180.0 * -1;
            double length1 = Math.Sqrt(Math.Pow(c2 - c1, 2) + Math.Pow(r2 - r1, 2));
            double length2 = Shape.Height * _k / 2;
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE2, row, column, phi, length1, length2);
            return obj;
        }
        /// <summary>
        /// 绘制圆形
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawCircle()
        {
            ConvertPoint(start_point.X, start_point.Y, out Point point);
            double row = point.Y;
            double column = point.X;
            double radius = Math.Sqrt(Math.Pow(c2 - c1, 2) + Math.Pow(r2 - r1, 2));
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE, row, column, radius);
            return obj;
        }
        /// <summary>
        /// 绘制椭圆
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawEllipse()
        {
            ConvertPoint(start_point.X, start_point.Y, out Point point);

            double row = point.Y;
            double column = point.X;
            // 计算长轴和短轴
            double radius1 = Math.Sqrt(Math.Pow(c2 - c1, 2) + Math.Pow(r2 - r1, 2));
            double radius2 = Shape.Height * _k / 2;
            double phi = angle * Math.PI / 180.0 * -1;  // 转换为弧度

            // 创建椭圆，使用实际的旋转角度和长短轴
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(
                HDrawingObject.HDrawingObjectType.ELLIPSE,
                row,
                column,
                phi,      // 旋转角度（弧度）
                radius1,  // 长轴
                radius2  // 短轴
            );
            return obj;
        }
        /// <summary>
        /// 绘制线
        /// </summary>
        /// <returns></returns>
        public HDrawingObject DrawLine()
        {
            double row1 = r1;
            double row2 = r2;
            double column1 = c1;
            double column2 = c2;
            HDrawingObject obj = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.LINE, row1, column1, row2, column2);
            return obj;
        }

        /// <summary>
        /// 判断绘制的图形是否有效
        /// </summary>
        /// <returns></returns>
        private bool IsValidShape()
        {
            double distance = Math.Sqrt(Math.Pow(c2 - c1, 2) + Math.Pow(r2 - r1, 2));
            switch (ShapeType)
            {
                case EShape.Rectangle1:
                    //宽或高小于一个像素
                    if (Math.Abs(r1 - r2) < 1 || Math.Abs(c1 - c2) < 1)
                    {
                        return false;
                    }
                    break;
                //这三种可以公用判断条件
                case EShape.Rectangle2:
                case EShape.Circle:
                case EShape.Ellipse:
                    //起始点到终止点的距离小于一个像素
                    if (distance < 1)
                    {
                        return false;
                    }
                    break;
                case EShape.Line:
                    //宽高同时小于一个像素
                    if (Math.Abs(r1 - r2) < 1 && Math.Abs(c1 - c2) < 1)
                    {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 初始化绘图形状
        /// </summary>
        private void InitializeShape()
        {
            switch (ShapeType)
            {
                case EShape.Rectangle1:
                    Shape = new Rectangle();
                    break;
                case EShape.Rectangle2:
                    Shape = new Rectangle();
                    break;
                case EShape.Circle:
                    Shape = new Ellipse();
                    break;
                case EShape.Ellipse:
                    Shape = new Ellipse();
                    break;
                case EShape.Line:
                    Shape = new Line();
                    break;
                default:
                    Shape = new Rectangle();
                    break;
            }
            Shape.Stroke = Brushes.Red;
            Shape.StrokeThickness = 1;
            Helper_Canvas.Children.Add(Shape);
        }

        private void Rectangle1_Button_Click(object sender, RoutedEventArgs e)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Rectangle1;
            InitializeShape();
        }
        private void Rectangle2_Button_Click(object sender, RoutedEventArgs e)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Rectangle2;
            InitializeShape();
        }
        private void Circle_Button_Click(object sender, RoutedEventArgs e)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Circle;
            InitializeShape();
        }
        private void Ellipse_Button_Click(object sender, RoutedEventArgs e)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Ellipse;
            InitializeShape();
        }

        private void Union_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            string url = "/Resources/union.png";
            SelectedModeImg = new BitmapImage(new Uri(url, UriKind.Relative));
            Mode = EROIMode.Union;
        }
        private void Intersection_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            string url = "/Resources/intersection.png";
            SelectedModeImg = new BitmapImage(new Uri(url, UriKind.Relative));
            Mode = EROIMode.Intersection;
        }
        private void Difference_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            string url = "/Resources/difference.png";
            SelectedModeImg = new BitmapImage(new Uri(url, UriKind.Relative));
            Mode = EROIMode.Difference;
        }

        private void Line_Button_Click(object sender, RoutedEventArgs e)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Line;
            InitializeShape();
        }

        /// <summary>
        /// 清除所有ROI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearAll_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var obj in DrawingObjects)
            {
                HWindow.DetachDrawingObjectFromWindow(obj);
                obj.Dispose();
            }
            DrawingObjects.Clear();
        }
        private void ClearCur_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedHDrawingObject == null)
            {
                return;
            }
            DrawingObjects.Remove(SelectedHDrawingObject);
            HWindow.DetachDrawingObjectFromWindow(SelectedHDrawingObject);
            SelectedHDrawingObject.Dispose();
            SelectedHDrawingObject = null;
        }
        /// <summary>
        /// 清除绘图图形
        /// </summary>
        private void ClearDrawData()
        {
            Helper_Canvas.Visibility = Visibility.Collapsed;
            Helper_Canvas.Children.Remove(Shape);
        }
    }

    public enum EShape
    {
        Rectangle1,
        Rectangle2,
        Circle,
        Ellipse,
        Line,
    }
    public enum EROIMode
    {
        Union,
        Difference,
        Intersection
    }

    public class ROIData : INotifyPropertyChanged
    {
        public ROIData(string name)
        {
            _name = name;
        }
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }
        private double _value;
        public double Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}