﻿using ImgurSniper.Libraries.Helper;
using ImgurSniper.Libraries.Native;
using ImgurSniper.Libraries.ScreenCapture;
using ImgurSniper.Properties;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using Rectangle = System.Drawing.Rectangle;

namespace ImgurSniper {
    /// <summary>
    ///     Interaction logic for ScreenshotWindow.xaml
    /// </summary>
    public partial class ScreenshotWindow : IDisposable {
        private bool _drag;

        public byte[] CroppedImage;
        public Point From, To;
        public string HwndName;
        public bool Error = true;

        //Size of current Mouse Location screen
        public static Rectangle Screen
            => System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;

        //Size of whole Screen Array
        public static Rectangle AllScreens => SystemInformation.VirtualScreen;


        public ScreenshotWindow() {
#if DEBUG
            Topmost = false;
#else
            Topmost = true;
#endif

            InitializeComponent();

            Position();
        }


        //Destructor
        ~ScreenshotWindow() {
            Dispose();
        }

        //Position Window correctly
        private void Position() {
            Rectangle size = ConfigHelper.AllMonitors ? AllScreens : Screen;

            Left = size.Left;
            Top = size.Top;
            Width = size.Width;
            Height = size.Height;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e) {
            selectionRectangle.CaptureMouse();

            //Set Position for Spacebar Menu
            Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            SelectedMode.Margin = new Thickness(
                bounds.Width / 2 - 50,
                bounds.Height / 2 - 25,
                bounds.Width / 2 - 50,
                bounds.Height / 2 - 25);

            //Activate & Focus Window
            Activate();
            Focus();

            //Hide in Alt + Tab Switcher View
            WindowInteropHelper wndHelper = new WindowInteropHelper(this);

            int exStyle = (int)NativeMethods.GetWindowLong(wndHelper.Handle, (int)NativeMethods.GetWindowLongFields.GwlExstyle);

            exStyle |= (int)NativeMethods.ExtendedWindowStyles.WsExToolwindow;
            NativeMethods.SetWindowLong(wndHelper.Handle, (int)NativeMethods.GetWindowLongFields.GwlExstyle, (IntPtr)exStyle);
        }

        //All Keys
        private void WindowKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    //Close
                    Error = false;
                    CloseSnap(false);
                    break;
                case Key.Space:
                    //Switch between Draw/Crop
                    SwitchMode();
                    break;
                case Key.A:
                    //Select All
                    if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                        SelectAllCmd();
                    break;
                case Key.Z:
                    //Undo
                    if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                        CtrlZ();
                    break;
            }
        }

        //Switch between Rectangle Snapping and Painting
        private void SwitchMode() {
            grid.IsEnabled = !grid.IsEnabled;
            PaintSurface.IsEnabled = !PaintSurface.IsEnabled;

            _currentPath = null;

            //"Hide" Selection Rectangle
            selectionRectangle.Margin = new Thickness(99999);

            //Stop animations by setting AnimationTimeline to null
            SelectedMode.BeginAnimation(OpacityProperty, null);

            //Set correct Selected Mode Indicator
            if (grid.IsEnabled) {
                //Prevent Painting in Capture Rectangle mode
                grid.CaptureMouse();
                Cursor = Cursors.Cross;
                CropIcon.Background = Brushes.Gray;
                DrawIcon.Background = Brushes.Transparent;
            } else {
                //Prevent Capturing Rectangle in Paint Mode
                PaintSurface.CaptureMouse();
                Cursor = Cursors.Pen;
                DrawIcon.Background = Brushes.Gray;
                CropIcon.Background = Brushes.Transparent;
            }

            //Fade Selected Mode View in
            FadeSelectedModeIn();
        }

        //Fade the Selected Mode (Drawing/Rectangle) in
        private async void FadeSelectedModeIn() {
            await SelectedMode.AnimateAsync(OpacityProperty, SelectedMode.Opacity, 0.9, 250);
            FadeSelectedModeOut();
        }

        //Fade the Selected Mode (Drawing/Rectangle) out
        private void FadeSelectedModeOut() {
            SelectedMode.Animate(OpacityProperty, SelectedMode.Opacity, 0, 250, 1000);
        }

        //Make image of whole Window with Ctrl + A
        private void SelectAllCmd() {
            selectionRectangle.Margin = new Thickness(0);

            From = new Point(0, 0);
            To = new Point(Width, Height);
            FinishRectangle();
        }

        #region Rectangle Mouse Events

        //MouseDown Event
        private void BeginRectangle(object sender, MouseButtonEventArgs e) {
            switch (e.ChangedButton) {
                case MouseButton.Right:
                    //Capture Window / Client Area of Window on Mouse Position
                    RightClick();
                    break;
                case MouseButton.Left:
                    //Lock the from Point to the Mouse Position when started holding Mouse Button
                    From = e.GetPosition(null);
                    break;
            }
        }

        //Mouse Move event (Commented out as much as possible for best Performance)
        private void MoveRectangle(object sender, MouseEventArgs e) {
            _drag = e.LeftButton == MouseButtonState.Pressed;

            //Draw Rectangle
            if (_drag) {
                //Set Crop Rectangle to Mouse Position
                To = e.GetPosition(null);

                //Width (w) and Height (h) of dragged Rectangle
                double w = Math.Abs(From.X - To.X);
                double h = Math.Abs(From.Y - To.Y);
                double left = Math.Min(From.X, To.X);
                double top = Math.Min(From.Y, To.Y);
                double right = Width - left - w;
                double bottom = Height - top - h;

                selectionRectangle.Margin = new Thickness(left, top, right, bottom);
            }
        }

        //MouseUp Event
        private void ReleaseRectangle(object sender, MouseButtonEventArgs e) {
            //Only trigger on Left Mouse Button
            if (e.ChangedButton != MouseButton.Left) {
                return;
            }

            To = e.GetPosition(null);
            FinishRectangle();
        }


        //Perform Right click -> Screenshot Window on cursor pos
        private async void RightClick() {
            Cursor = Cursors.Hand;

            NativeMethods.GetCursorPos(out NativeStructs.POINT point);

            //Fade out
            await grid.AnimateAsync(OpacityProperty, grid.Opacity, 0, 250);

            Topmost = false;
            Opacity = 0;

            //For render complete
            await Dispatcher.InvokeAsync(new Action(() => { }), DispatcherPriority.ContextIdle);
            await Task.Delay(50);

            //Send Window to back, so WinAPI.User32.WindowFromPoint does not detect ImgurSniper as Window
            NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, NativeMethods.HwndBottom, 0, 0, 0, 0,
                NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNoactivate);

            IntPtr whandle = NativeMethods.WindowFromPoint(point);

            NativeMethods.SetForegroundWindow(whandle);
            NativeMethods.SetActiveWindow(whandle);

            Rectangle hwnd = NativeMethods.GetWindowRectangle(whandle);

            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            if (NativeMethods.GetWindowText(whandle, buff, nChars) > 0) {
                HwndName = buff.ToString();
            }

            Point to = new Point(hwnd.Left + hwnd.Width, hwnd.Top + hwnd.Height);
            Point from = new Point(hwnd.Left, hwnd.Top);

            Crop(from, to);
        }

        #endregion

        #region Painting Mouse Events

        private Point _startPos;
        private Path _currentPath;

        //Mouse Down Event - Begin Painting
        private void BeginPaint(object sender, MouseButtonEventArgs e) {
            if (e.ButtonState == MouseButtonState.Pressed) {
                _startPos = e.GetPosition(null);


                _currentPath = new Path {
                    Data = new PathGeometry {
                        Figures = {
                            new PathFigure {
                                StartPoint = _startPos,
                                Segments = {new PolyLineSegment()}
                            }
                        }
                    },
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 4
                };

                PaintSurface.Children.Add(_currentPath);
            }
        }

        //Mouse Move Event - Draw on the Window
        private void Paint(object sender, MouseEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                if (_currentPath == null) {
                    return;
                }

                PolyLineSegment pls =
                    (PolyLineSegment)((PathGeometry)_currentPath.Data).Figures.Last().Segments.Last();
                pls.Points.Add(e.GetPosition(null));
            }
        }

        //Mouse Up Event - Stop Painting
        private void StopPaint(object sender, MouseButtonEventArgs e) {
            _currentPath = null;
        }

        //Ctrl + Z - Undo Last Paint Stroke
        private void CtrlZ() {
            if (PaintSurface.Children.Count > 0) {
                PaintSurface.Children.RemoveAt(PaintSurface.Children.Count - 1);
            }
        }

        #endregion

        #region Snap Helper

        //Finish drawing Rectangle
        private async void FinishRectangle() {
            //From and To Point -> PointToScreen for different DPI
            Point from = new Point((int)Math.Min(From.X, To.X), (int)Math.Min(From.Y, To.Y));
            Point to = new Point((int)Math.Max(From.X, To.X), (int)Math.Max(From.Y, To.Y));
            from = PointToScreen(from);
            to = PointToScreen(to);

            if (Math.Abs(To.X - From.X) < 9 || Math.Abs(To.Y - From.Y) < 9) {
                // Too small
                selectionRectangle.Margin = new Thickness(99999);
            } else {
                //Prevent input
                IsEnabled = false;

                Cursor = Cursors.Arrow;

                //Fade out animation
                await grid.AnimateAsync(OpacityProperty, grid.Opacity, 0, 150);
                //Fade out render complete
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                await Task.Delay(100);

                //Crop Image
                Crop(from, to);
            }
        }

        //Make Image from custom Coords
        private void Crop(Point from, Point to) {
            int w = (int)(to.X - from.X);
            int h = (int)(to.Y - from.Y);

            try {
                Rectangle size = new Rectangle((int)from.X, (int)from.Y, w, h);

                using (Image img = Screenshot.GetScreenshotNative(NativeMethods.GetDesktopWindow(), size, ConfigHelper.ShowMouse)) {
                    if (ConfigHelper.Compression < 100) {
                        using (MemoryStream stream = ImageHelper.CompressImage(img, ConfigHelper.ImageFormat, ConfigHelper.Compression)) {
                            CroppedImage = stream.ToArray();
                        }
                    } else {
                        using (MemoryStream stream = new MemoryStream()) {
                            img.Save(stream, ConfigHelper.ImageFormat);
                            CroppedImage = stream.ToArray();
                        }
                    }
                }
                CloseSnap(true, 0);
            } catch {
                CloseSnap(false, 0);
            }
        }

        //Close Window with fade out animation
        private async void CloseSnap(bool result, int delay = 0) {
            await this.AnimateAsync(OpacityProperty, Opacity, 0, 150, delay);

            try {
                if (result) {
                    await ScreenshotHelper.FinishScreenshot(CroppedImage, HwndName);
                    DialogResult = true;
                    return;
                } else {
                    if (Error)
                        await Statics.ShowNotificationAsync(strings.uploadingError, NotificationWindow.NotificationType.Error);
                }
            } catch {
                // could not finish screenshot
            }
            DialogResult = false;
        }

        #endregion

        //Release any Resources
        public void Dispose() {
            CroppedImage = null;

            try {
                Dispatcher.Invoke(Close);
            } catch {
                //Window already closed
            }

            GC.Collect();
        }
    }
}