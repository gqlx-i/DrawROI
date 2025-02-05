using HalconDotNet;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Documents.Serialization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;
using static HalconDotNet.HDrawingObject;

namespace WpfApp1
{
    /// <summary>
    /// ROICanvas.xaml 的交互逻辑
    /// </summary>
    public partial class ROICanvas : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public HRegion CrossLine;
        private HRegion _displayCrossLine;
        /// <summary>
        /// 十字辅助线(界面绑定)
        /// </summary>
        public HRegion DisplayCrossLine
        {
            get => _displayCrossLine;
            set
            {
                if (_displayCrossLine != value)
                {
                    _displayCrossLine = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayCrossLine)));
                }
            }
        }

        #region 依赖属性
        /// <summary>
        /// 是否显示十字辅助线
        /// </summary>
        public bool IsShowCrossLine
        {
            get { return (bool)GetValue(IsShowCrossLineProperty); }
            set { SetValue(IsShowCrossLineProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsShowCrossLine.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsShowCrossLineProperty =
            DependencyProperty.Register("IsShowCrossLine", typeof(bool), typeof(ROICanvas), new PropertyMetadata(false, OnIsShowCrossLineChanged));
        
        private static void OnIsShowCrossLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ROICanvas;
            bool newval = (bool)e.NewValue;
            if (newval)
            {
                control!.DisplayCrossLine = control!.CrossLine;
            }
            else
            {
                control!.DisplayCrossLine = null;
            }
        }

        /// <summary>
        /// 显示的图像
        /// </summary>
        public HImage DisplayImage
        {
            get { return (HImage)GetValue(DisplayImageProperty); }
            set { SetValue(DisplayImageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DisplayImage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DisplayImageProperty =
            DependencyProperty.Register("DisplayImage", typeof(HImage), typeof(ROICanvas), new PropertyMetadata(null, OnDisplayImageChanged));
        private static void OnDisplayImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ROICanvas;
            var oldImg = e.OldValue as HImage;
            var newImg = e.NewValue as HImage;
            if (newImg != null)
            {
                newImg.GetImageSize(out HTuple width, out HTuple height);

                // 计算精确的中心点
                double centerX = width.D / 2.0;
                double centerY = height.D / 2.0;

                // 生成竖线
                HRegion verticalLine = new HRegion();
                verticalLine.GenRegionLine(0, centerX, height.D, centerX);

                // 生成横线
                HRegion horizontalLine = new HRegion();
                horizontalLine.GenRegionLine(centerY, 0, centerY, width.D);

                // 合并两条线
                control?.CrossLine?.Dispose();
                control!.CrossLine = verticalLine.ConcatObj(horizontalLine);

                // 释放临时区域
                verticalLine.Dispose();
                horizontalLine.Dispose();
                if (control!.IsShowCrossLine)
                {
                    control!.DisplayCrossLine = control!.CrossLine;
                }
            }
            control.HWindow?.DetachBackgroundFromWindow();
            control.ClearAllROI(null);
        }
        #endregion

        #region 菜单按钮
        //区域融合
        public ICommand SelectMergeModeCommand { get; set; }
        //矩形
        public ICommand CreateRectangle1Command { get; set; }
        //旋转矩形
        public ICommand CreateRectangle2Command { get; set; }
        //圆形
        public ICommand CreateCircleCommand { get; set; }
        //椭圆
        public ICommand CreateEllipseCommand { get; set; }
        //直线
        public ICommand CreateLineCommand { get; set; }
        //清除当前ROI
        public ICommand ClearCurROICommand { get; set; }
        //清除所有ROI
        public ICommand ClearAllROICommand { get; set; }
        #endregion

        EShape ShapeType;
        Shape Shape;
        HRegion CurRegion = new HRegion();
        string CurRegionColor = "#ffff0040";
        HXLDCont CurXLDCont = new HXLDCont();
        string CurXLDContColor = "green";
        ERegionMergeMode Mode = ERegionMergeMode.Union; 

        //roi集合
        public List<HDrawingObjectData> HDrawingObjects { get; set; } = new List<HDrawingObjectData>();
        HDrawingObject SelectedHDrawingObject;
        string HDrawingObjectColor = "red";
        //roi数据集合
        public ObservableCollection<ROIData> ROIDatas { get; set; } = new ObservableCollection<ROIData>();

        string SelectedROIType;

        HWindow HWindow;

        bool isMouseDown = false;

        private double _k = 1;
        private double _offsetX = 0;
        private double _offsetY = 0;

        public Point start_point, end_point;
        double r1, r2, c1, c2;
        double angle = 0;

        private BitmapImage _selectedModeImg = new BitmapImage(new Uri("/Resources/Union.png", UriKind.Relative));
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

            SelectMergeModeCommand = new DelegateCommand(SelectMergeMode);
            CreateRectangle1Command = new DelegateCommand(CreateRectangle1);
            CreateRectangle2Command = new DelegateCommand(CreateRectangle2);
            CreateCircleCommand = new DelegateCommand(CreateCircle);
            CreateEllipseCommand = new DelegateCommand(CreateEllipse);
            CreateLineCommand = new DelegateCommand(CreateLine);
            ClearCurROICommand = new DelegateCommand(ClearCurROI);
            ClearAllROICommand = new DelegateCommand(ClearAllROI);

            this.DataContext = this;
        }

        /// <summary>
        /// halcon窗体初始化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SmartControl_HInitWindow(object sender, EventArgs e)
        {
            HWindow = SmartControl.HalconWindow;

            //参考文档网址：https://blog.csdn.net/sinat_21001719/article/details/128647619
            #region 比例与偏移计算
            var dpd = DependencyPropertyDescriptor.FromProperty(HSmartWindowControlWPF.HImagePartProperty, typeof(HSmartWindowControlWPF));
            dpd.AddValueChanged(SmartControl, (o, es) =>
            {
                var imgPart = SmartControl.HImagePart;
                _k = imgPart.Height / SmartControl.ActualHeight;
                _offsetX = imgPart.X;
                _offsetY = imgPart.Y;
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
                //设置HDrawingObject样式
                obj.SetDrawingObjectParams("color", HDrawingObjectColor);
                //obj.SetDrawingObjectParams("line_width", 1);
                //obj.SetDrawingObjectParams(new HTuple("line_style"), new HTuple(10));
                HDrawingObjects.Add(new HDrawingObjectData(obj, Mode));
                
                //设置回调
                obj.OnDrag(HDrawingObjectCallbackClass);
                obj.OnResize(HDrawingObjectCallbackClass);
                obj.OnAttach(HDrawingObjectCallbackClass);
                obj.OnSelect(HDrawingObjectCallbackClass);
                obj.OnDetach(HDrawingObjectCallbackClass);

                //必须放在OnAttach后附加obj才会触发回调函数
                HWindow.AttachDrawingObjectToWindow(obj);
            }
            ClearDrawData();

            //HHomMat2D homMat2D = new HHomMat2D();
            //homMat2D.HomMat2dTranslate(_offsetX, _offsetY);
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
            
            //清除窗口
            HWindow.ClearWindow();
            //显示图像
            HWindow.AttachBackgroundToWindow(DisplayImage);
            if (IsShowCrossLine)
            {
                CrossLine.DispObj(HWindow);
            }
            //生成融合后区域
            GenRegions();
            CreateTemplate();
        }

        /// <summary>
        /// 生成融合后区域
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        private void GenRegions()
        {
            //释放上一次Curregion
            CurRegion?.Dispose();
            CurRegion = null;
            bool isFirst = true;
            foreach (var item in HDrawingObjects)
            {
                HObject obj = item.HDrawingObject.GetDrawingObjectIconic();      
                HRegion region = new HRegion(obj);

                if (isFirst)
                {
                    CurRegion = region;
                    isFirst = false;
                }
                else
                {
                    RegionMerge(region, item.Mode);
                    //第一个区域的地址先不释放
                    region.Dispose();
                }
                obj.Dispose();
                obj = null;
            }
            //设置区域颜色
            HWindow.SetColor(CurRegionColor);
            //显示融合后区域
            CurRegion?.DispObj(HWindow);
        }
        /// <summary>
        /// 区域融合
        /// </summary>
        /// <param name="region"></param>
        private void RegionMerge(HRegion region, ERegionMergeMode mode)
        {
            switch (mode)
            {
                case ERegionMergeMode.Union:
                    CurRegion = CurRegion.Union2(region);
                    break;
                case ERegionMergeMode.Difference:
                    CurRegion = CurRegion.Difference(region);
                    break;
                case ERegionMergeMode.Intersection:
                    CurRegion = CurRegion.Intersection(region);
                    break;
                case ERegionMergeMode.XOR:
                    CurRegion = CurRegion.SymmDifference(region);
                    break;
                default:
                    break;
            }
        }
        /// <summary>
        /// 创建模板
        /// </summary>
        /// <param name="parameter"></param>
        private void CreateTemplate()
        {
            try
            {
                HImage reduceImage = DisplayImage.ReduceDomain(CurRegion);
                HShapeModel hShapeModel = new HShapeModel();
                hShapeModel.CreateShapeModel(reduceImage, "auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
                hShapeModel.GetShapeModelParams(out double angleStart, out double angleExtent, out double angleStep, out HTuple scaleMin,
                                    out HTuple scaleMax, out HTuple scaleStep, out string metric, out int minContrast);
                CurXLDCont?.Dispose();
                HXLDCont tempXLD = hShapeModel.GetShapeModelContours(1);
                DisplayImage.GetImageSize(out HTuple width, out HTuple height);
                //因为ROI可能会移出图像外，所以需要与图像本身做交集
                HRegion imgRegion = new HRegion(0, 0, height - 1, width - 1);
                HRegion mergedRegion = imgRegion.Intersection(CurRegion);
                //计算区域中心
                mergedRegion.AreaCenter(out double row, out double col);
                //把模板轮廓从0，0点移动到区域中心
                HHomMat2D hHomMat2D = new HHomMat2D();
                hHomMat2D.VectorAngleToRigid(0, 0, 0, row, col, 0);
                CurXLDCont = hHomMat2D.AffineTransContourXld(tempXLD);

                width.Dispose();
                height.Dispose();
                imgRegion.Dispose();
                mergedRegion.Dispose();
                reduceImage.Dispose();
                hShapeModel.Dispose();
                tempXLD.Dispose();
                HWindow.SetColor(CurXLDContColor);
                CurXLDCont.DispObj(HWindow);
            }
            catch (Exception ex)
            {

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
                X = _k * px + _offsetX,
                Y = _k * py + _offsetY
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
            HDrawingObject obj = CreateDrawingObject(HDrawingObjectType.RECTANGLE1, row1, column1, row2, column2);
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
            HDrawingObject obj = CreateDrawingObject(HDrawingObjectType.RECTANGLE2, row, column, phi, length1, length2);
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
            HDrawingObject obj = CreateDrawingObject(HDrawingObjectType.CIRCLE, row, column, radius);
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
            HDrawingObject obj = CreateDrawingObject(
                HDrawingObjectType.ELLIPSE,
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
            HDrawingObject obj = CreateDrawingObject(HDrawingObjectType.LINE, row1, column1, row2, column2);
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

        private void CreateRectangle1(object parameter)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Rectangle1;
            InitializeShape();
        }
        private void CreateRectangle2(object parameter)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Rectangle2;
            InitializeShape();
        }
        private void CreateCircle(object parameter)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Circle;
            InitializeShape();
        }
        private void CreateEllipse(object parameter)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Ellipse;
            InitializeShape();
        }
        private void CreateLine(object parameter)
        {
            Helper_Canvas.Visibility = Visibility.Visible;
            ShapeType = EShape.Line;
            InitializeShape();
        }

        /// <summary>
        /// 设置区域融合模式
        /// </summary>
        /// <param name="parameter">图标名</param>
        private void SelectMergeMode(object parameter)
        {
            string url = $"/Resources/{parameter}.png";
            SelectedModeImg = new BitmapImage(new Uri(url, UriKind.Relative));
            Mode = (ERegionMergeMode)parameter;
        }

        /// <summary>
        /// 清除所有ROI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearAllROI(object parameter)
        {
            while (HDrawingObjects.Count > 0)
            {
                var item = HDrawingObjects[0];
                //必须先移除再取消附加，这样界面不会有残留
                HDrawingObjects.Remove(item);
                HWindow.DetachDrawingObjectFromWindow(item.HDrawingObject);
                item.HDrawingObject.Dispose();
                item.HDrawingObject = null;
            }
            ROIDatas.Clear();
        }
        /// <summary>
        /// 清除当前ROI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearCurROI(object parameter)
        {
            if (SelectedHDrawingObject == null)
            {
                return;
            }
            //必须先移除再取消附加，这样界面不会有残留
            HDrawingObjects.RemoveAll(t => t.HDrawingObject.ID == SelectedHDrawingObject.ID);
            HWindow.DetachDrawingObjectFromWindow(SelectedHDrawingObject);
            SelectedHDrawingObject.Dispose();
            SelectedHDrawingObject = null;
            ROIDatas.Clear();
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
    public enum ERegionMergeMode
    {
        Union,
        Difference,
        Intersection,
        XOR
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
    public class HDrawingObjectData
    {
        public HDrawingObject HDrawingObject { get; set; }
        public ERegionMergeMode Mode { get; set; }
        public HDrawingObjectData(HDrawingObject obj, ERegionMergeMode mode)
        {
            HDrawingObject = obj;
            Mode = mode;
        }
    }
}
