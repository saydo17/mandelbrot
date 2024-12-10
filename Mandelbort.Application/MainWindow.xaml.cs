using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Numerics;
using System.Threading;
using System.Windows.Data;
using System.Windows.Threading;

namespace Mandelbort.Application
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly int _pixelWidth;
        private readonly int _pixelHeight;


        public static readonly DependencyProperty MaxIterationsProperty = DependencyProperty.Register(
            "MaxIterations", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(1000)
            {
                BindsTwoWayByDefault = true,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        public int MaxIterations
        {
            get { return (int) GetValue(MaxIterationsProperty); }
            set { SetValue(MaxIterationsProperty, value); }
        }
        public static readonly DependencyProperty CenterXProperty = DependencyProperty.Register(
            "CenterX", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(-0.745428)
            {
                BindsTwoWayByDefault = true,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        public double CenterX
        {
            get { return (double) GetValue(CenterXProperty); }
            set { SetValue(CenterXProperty, value); }
        }

        public static readonly DependencyProperty CenterYProperty = DependencyProperty.Register(
            "CenterY", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(0.113009)
            {
                BindsTwoWayByDefault = true,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        public double CenterY
        {
            get { return (double) GetValue(CenterYProperty); }
            set { SetValue(CenterYProperty, value); }
        }

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
            "Zoom", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(3.0E-5)
            {
                BindsTwoWayByDefault = true,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        public double Zoom
        {
            get { return (double) GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        public WriteableBitmap MandelbrotBitmapSource { get; }

        public MainWindow()
        {
            //RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            //RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            InitializeComponent();
            _pixelWidth = 1024*4;
            _pixelHeight = 1024*4;
            _emptyImage = new int[_pixelWidth * _pixelHeight];
            MandelbrotBitmapSource = new WriteableBitmap(_pixelWidth, _pixelHeight, 96, 96, PixelFormats.Bgr32, null);
            MandelbrotContainer.Source = MandelbrotBitmapSource;
        }

        private readonly int[] _emptyImage;

        private struct Chunk
        {
            public Chunk(int a, int b)
            {
                A = a;
                B = b;
            }

            public int A { get; }
            public int B { get; }
        }
        private void DrawMandelbrot()
        {
            double minX = 0;
            double minY = 0;
            int maxIterations = 0;
            double zoom = 0;
            Dispatcher.Invoke(() =>
            {
                minX = CenterX - Zoom / 2;
                minY = CenterY - Zoom / 2;
                maxIterations = MaxIterations;
                zoom = Zoom;
            });


            var buffer = new int[_pixelHeight*_pixelWidth];
            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 4};
            var subPixelWidth = _pixelWidth / 8;
            var subPixelHeight = _pixelHeight / 8;
            var chunks = new List<Chunk>();
            for (int a = 0; a < 8; a++)
            {
                for (int b = 0; b < 8; b++)
                {
                    chunks.Add(new Chunk(a,b));
                }
            }

            Parallel.ForEach(chunks, chunk =>
            {
                for (int i = subPixelWidth * chunk.A; i < subPixelWidth * chunk.A + subPixelWidth; i++)
                {
                    double x = minX + (double)i / _pixelWidth * zoom;
                    for (int j = subPixelHeight * chunk.B; j < subPixelHeight * chunk.B + subPixelHeight; j++)
                    {
                        double y = minY + (double)j / _pixelHeight * zoom;

                        var iterations = Mandelbrot(x, y, maxIterations);
                        var color = MandelColor.FromIterations(iterations, maxIterations);
                        buffer[i + j * _pixelWidth] = color.Rgb();


                    }

                    Dispatcher.Invoke(() => PlotRegion(buffer, chunk.A * subPixelWidth, chunk.B * subPixelHeight),
                        DispatcherPriority.Render);
                }
            });

            Dispatcher.Invoke(() => DrawButton.IsEnabled = true);
        }
        
        private void ClearPlot()
        {
            try
            {
                MandelbrotBitmapSource.Lock();
                var stride = _pixelWidth * 4 + (_pixelWidth % 4);
                MandelbrotBitmapSource.WritePixels(new Int32Rect(0, 0, _pixelWidth, _pixelHeight), _emptyImage, stride,
                    0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                MandelbrotBitmapSource.Unlock();
            }
        }
        private void PlotRegion(Array buffer, int x, int y)
        {
            try
            {

                MandelbrotBitmapSource.Lock();
                var stride = _pixelWidth * 4 + (_pixelWidth % 4);

                var sourceRect = new Int32Rect(x, y, _pixelWidth/8, _pixelHeight/8);
                MandelbrotBitmapSource.WritePixels(sourceRect, buffer,
                    stride, x,y);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                MandelbrotBitmapSource.Unlock();
            }
        }
        private void PlotAll(Array buffer)
        {
            try
            {

                MandelbrotBitmapSource.Lock();
                var stride = _pixelWidth * 4 + (_pixelWidth % 4);
                var length = _pixelWidth * _pixelHeight * 4;

                MandelbrotBitmapSource.WritePixels(new Int32Rect(0, 0, _pixelWidth, _pixelHeight), buffer,
                    stride, 0);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                MandelbrotBitmapSource.Unlock(); 
            }
        }


        private struct MandelColor
        {
            private static readonly GradientStopCollection Gradient;
            public static MandelColor Black => new MandelColor(0, 0, 0);

            private static Color GetColor(double value)
            {

                value = 1-value;
                return GetRelativeColor(Gradient, value);

                const double maxColor = 256;
                const double contrastValue = 0.2;
                
                return Color.FromRgb(0, 0,
                    (byte)(maxColor * Math.Pow(value, contrastValue)));
            }
            public static Color GetRelativeColor(GradientStopCollection gsc, double offset)
            {
                var point = gsc.SingleOrDefault(f => f.Offset == offset);
                if (point != null) return point.Color;

                GradientStop before = gsc.Where(w => w.Offset == gsc.Min(m => m.Offset)).First();
                GradientStop after = gsc.Where(w => w.Offset == gsc.Max(m => m.Offset)).First();

                foreach (var gs in gsc)
                {
                    if (gs.Offset < offset && gs.Offset > before.Offset)
                    {
                        before = gs;
                    }
                    if (gs.Offset > offset && gs.Offset < after.Offset)
                    {
                        after = gs;
                    }
                }

                var color = new Color();

                color.ScA = (float)((offset - before.Offset) * (after.Color.ScA - before.Color.ScA) / (after.Offset - before.Offset) + before.Color.ScA);
                color.ScR = (float)((offset - before.Offset) * (after.Color.ScR - before.Color.ScR) / (after.Offset - before.Offset) + before.Color.ScR);
                color.ScG = (float)((offset - before.Offset) * (after.Color.ScG - before.Color.ScG) / (after.Offset - before.Offset) + before.Color.ScG);
                color.ScB = (float)((offset - before.Offset) * (after.Color.ScB - before.Color.ScB) / (after.Offset - before.Offset) + before.Color.ScB);

                return color;
            }

            public static MandelColor FromIterations(double iterations, int maxIterations)
            {
                
                if (iterations == maxIterations) return Black;
                var tempColor = GetColor(iterations / maxIterations);
                return new MandelColor(tempColor.R, tempColor.G, tempColor.B);
            
            }

            public MandelColor(byte red, byte green, byte blue)
            {
                Red = red;
                Green = green;
                Blue = blue;
            }

            static MandelColor()
            {
                Gradient = new GradientStopCollection(10)
                {
                    new GradientStop(Colors.Red, 0),
                    new GradientStop(Colors.OrangeRed, 0.1),
                    new GradientStop(Colors.Orange, 0.2),
                    new GradientStop(Colors.Yellow, 0.3),
                    new GradientStop(Colors.YellowGreen, 0.4),
                    new GradientStop(Colors.Green, 0.5),
                    new GradientStop(Colors.Teal, 0.6),
                    new GradientStop(Colors.Blue, 0.7),
                    new GradientStop(Colors.BlueViolet, 0.8),
                    new GradientStop(Colors.DarkViolet, 0.9),
                    new GradientStop(Colors.DarkMagenta, 1)
                };
                Gradient.Freeze();
            }

            public byte Red { get; }
            public byte Green { get; }
            public byte Blue { get; }

            public int Rgb() => Red << 16 | Green << 8 | Blue << 0;
        }
        
        private void Plot(int x, int y, MandelColor color)
        {
            try
            {
                var backBuffer = new IntPtr(0);
                var backBufferStride = 0;

                Dispatcher.Invoke(() =>
                {
                    MandelbrotBitmapSource.Lock();
                    backBuffer = MandelbrotBitmapSource.BackBuffer;
                    backBufferStride = MandelbrotBitmapSource.BackBufferStride;
                });
                
                unsafe
                {
                    
                    backBuffer += y * backBufferStride;
                    backBuffer += x * 4;
                    *((int*) backBuffer) = color.Rgb();
                    
                }

                Dispatcher.Invoke(() =>
                {
                    MandelbrotBitmapSource.AddDirtyRect(new Int32Rect(x, y, 1, 1));
                    MandelbrotBitmapSource.Unlock();
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private double Mandelbrot(double x, double y, int maxIterations)
        {
            var c = new Complex(x, y);

            var iterations = 0;
            var z = new Complex(0, 0);
            do
            {
                z = Complex.Add(Complex.Multiply(z, z), c);
                iterations++;
            } while (z.Magnitude <= 4 && iterations < maxIterations);


            if (iterations == maxIterations) return maxIterations;
            var normalized = iterations + 1 - Math.Log10(Math.Log(Complex.Abs(z), 2));
            return normalized;
        }


        private void OnDraw(object sender, RoutedEventArgs e)
        {
            ClearPlot();
            DrawButton.IsEnabled = false;
            var thread = new Thread(DrawMandelbrot);
            thread.Start();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var fileInfo = new FileInfo("mandelbrot.png");
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            
            using (FileStream stream =
                new FileStream(Path.Combine(path, fileInfo.Name), FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(MandelbrotBitmapSource));
                encoder.Save(stream);
            }
        }
    }
}