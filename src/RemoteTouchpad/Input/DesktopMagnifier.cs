using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RemoteTouchpad.Input;

public sealed class DesktopMagnifier
{
    private readonly object _lock = new();
    private Thread? _thread;
    private MagnifierForm? _form;
    private bool _starting;
    private MagnifierOptions _options = MagnifierOptions.Default;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _starting || _form is not null;
            }
        }
    }

    public void Configure(double zoom, int size)
    {
        var options = MagnifierOptions.Create(zoom, size);
        MagnifierForm? form;
        lock (_lock)
        {
            _options = options;
            form = _form;
        }

        AppLogger.Info($"Desktop magnifier configured. Zoom={options.Zoom}, Size={options.Size}, CaptureSize={options.CaptureSize}");
        if (form is not null && form.IsHandleCreated)
        {
            try
            {
                form.BeginInvoke(new Action(() => form.UpdateOptions(options)));
            }
            catch (InvalidOperationException ex)
            {
                AppLogger.Error("Desktop magnifier configure failed while dispatching to form.", ex);
            }
        }
    }

    public void Toggle()
    {
        AppLogger.Info($"Desktop magnifier toggle requested. Running={IsRunning}");
        if (IsRunning)
        {
            Stop();
            return;
        }

        Start();
    }

    public void Start()
    {
        lock (_lock)
        {
            AppLogger.Info($"Desktop magnifier start requested. Starting={_starting}, HasForm={_form is not null}, Zoom={_options.Zoom}, Size={_options.Size}");
            if (_starting || _form is not null)
            {
                return;
            }

            _starting = true;
            _thread = new Thread(RunMagnifier)
            {
                IsBackground = true,
                Name = "RemoteTouchpadMagnifier"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    public void Stop()
    {
        MagnifierForm? form;
        lock (_lock)
        {
            AppLogger.Info($"Desktop magnifier stop requested. Starting={_starting}, HasForm={_form is not null}");
            _starting = false;
            form = _form;
        }

        if (form is null)
        {
            return;
        }

        try
        {
            if (form.IsHandleCreated)
            {
                form.BeginInvoke(new Action(form.Close));
            }
        }
        catch (InvalidOperationException ex)
        {
            AppLogger.Error("Desktop magnifier stop failed while closing form.", ex);
        }
    }

    private void RunMagnifier()
    {
        AppLogger.Info("Desktop magnifier thread started.");
        try
        {
            MagnifierOptions options;
            lock (_lock)
            {
                options = _options;
            }

            using var form = new MagnifierForm(options);
            lock (_lock)
            {
                _form = form;
                _starting = false;
            }

            form.FormClosed += (_, _) =>
            {
                AppLogger.Info("Desktop magnifier form closed.");
                lock (_lock)
                {
                    _form = null;
                    _thread = null;
                    _starting = false;
                }
            };

            AppLogger.Info("Desktop magnifier message loop starting.");
            Application.Run(form);
            AppLogger.Info("Desktop magnifier message loop ended.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Desktop magnifier thread crashed.", ex);
            lock (_lock)
            {
                _form = null;
                _thread = null;
                _starting = false;
            }
        }
    }

    private readonly record struct MagnifierOptions(double Zoom, int Size)
    {
        public static MagnifierOptions Default => new(2.0, 260);

        public int CaptureSize => Math.Max(1, (int)Math.Round(Size / Zoom));

        public static MagnifierOptions Create(double zoom, int size)
        {
            return new MagnifierOptions(
                Math.Round(Math.Clamp(zoom, 1.25, 4.0), 2),
                Math.Clamp(size, 160, 420));
        }
    }

    private sealed class MagnifierForm : Form
    {
        private const int CursorOffset = 28;
        private const int SwpNoActivate = 0x0010;
        private const int SwpNoOwnerZOrder = 0x0200;
        private const int SwpShowWindow = 0x0040;
        private static readonly nint HwndTopmost = new(-1);

        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 55 };
        private MagnifierOptions _options;
        private bool _hasShown;
        private string? _lastPaintError;

        public MagnifierForm(MagnifierOptions options)
        {
            _options = options;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = true;
            ApplySize();

            _timer.Tick += (_, _) =>
            {
                try
                {
                    MoveNearCursor();
                    RefreshTopMost();
                    Invalidate();
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Desktop magnifier timer tick failed.", ex);
                }
            };
            _timer.Start();
            AppLogger.Info($"Desktop magnifier form created. Zoom={_options.Zoom}, Size={_options.Size}, CaptureSize={_options.CaptureSize}");
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int wsExNoActivate = 0x08000000;
                const int wsExToolWindow = 0x00000080;
                const int wsExTopmost = 0x00000008;
                var createParams = base.CreateParams;
                createParams.ExStyle |= wsExNoActivate | wsExToolWindow | wsExTopmost;
                return createParams;
            }
        }

        public void UpdateOptions(MagnifierOptions options)
        {
            _options = options;
            ApplySize();
            MoveNearCursor();
            RefreshTopMost();
            Invalidate();
            AppLogger.Info($"Desktop magnifier options applied. Zoom={_options.Zoom}, Size={_options.Size}, CaptureSize={_options.CaptureSize}");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshTopMost();
            if (!_hasShown)
            {
                _hasShown = true;
                AppLogger.Info($"Desktop magnifier form shown. Bounds={Bounds}, VirtualScreen={SystemInformation.VirtualScreen}, Zoom={_options.Zoom}, Size={_options.Size}");
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            try
            {
                PaintMagnifiedContent(e.Graphics);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (!string.Equals(_lastPaintError, message, StringComparison.Ordinal))
                {
                    _lastPaintError = message;
                    AppLogger.Error("Desktop magnifier paint failed.", ex);
                }

                PaintFallback(e.Graphics, _options.Size);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AppLogger.Info("Desktop magnifier form closing cleanup.");
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        private void ApplySize()
        {
            var size = _options.Size;
            Size = new Size(size, size);

            Region?.Dispose();
            using var path = CreateLensPath(size);
            Region = new Region(path);
        }

        private void PaintMagnifiedContent(Graphics target)
        {
            var cursor = Cursor.Position;
            var bounds = SystemInformation.VirtualScreen;
            var captureSize = _options.CaptureSize;
            var source = ClampRectangle(
                new Rectangle(cursor.X - captureSize / 2, cursor.Y - captureSize / 2, captureSize, captureSize),
                bounds);

            using var bitmap = new Bitmap(source.Width, source.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(source.Location, Point.Empty, source.Size);
            }

            target.SmoothingMode = SmoothingMode.AntiAlias;
            target.InterpolationMode = InterpolationMode.NearestNeighbor;
            target.DrawImage(bitmap, ClientRectangle);
            DrawLensChrome(target, _options.Size);
        }

        private static void PaintFallback(Graphics target, int lensSize)
        {
            target.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(230, 20, 28, 40));
            target.FillEllipse(brush, 0, 0, lensSize, lensSize);
            DrawLensChrome(target, lensSize);
        }

        private static void DrawLensChrome(Graphics graphics, int lensSize)
        {
            using var shade = new SolidBrush(Color.FromArgb(24, 9, 16, 28));
            graphics.FillEllipse(shade, 0, 0, lensSize, lensSize);

            using var outerPen = new Pen(Color.FromArgb(240, 246, 250, 255), 4);
            using var middlePen = new Pen(Color.FromArgb(210, 66, 132, 210), 2);
            using var innerPen = new Pen(Color.FromArgb(150, 10, 18, 32), 1);
            graphics.DrawEllipse(outerPen, 2, 2, lensSize - 5, lensSize - 5);
            graphics.DrawEllipse(middlePen, 8, 8, lensSize - 17, lensSize - 17);
            graphics.DrawEllipse(innerPen, 13, 13, lensSize - 27, lensSize - 27);

            using var crossPen = new Pen(Color.FromArgb(170, 246, 250, 255), 1);
            var center = lensSize / 2;
            graphics.DrawLine(crossPen, center - 14, center, center + 14, center);
            graphics.DrawLine(crossPen, center, center - 14, center, center + 14);
        }

        private void MoveNearCursor()
        {
            var cursor = Cursor.Position;
            var bounds = SystemInformation.VirtualScreen;
            var left = cursor.X + CursorOffset;
            var top = cursor.Y + CursorOffset;

            if (left + Width > bounds.Right)
            {
                left = cursor.X - Width - CursorOffset;
            }

            if (top + Height > bounds.Bottom)
            {
                top = cursor.Y - Height - CursorOffset;
            }

            left = Math.Clamp(left, bounds.Left, bounds.Right - Width);
            top = Math.Clamp(top, bounds.Top, bounds.Bottom - Height);
            Location = new Point(left, top);
        }

        private void RefreshTopMost()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            SetWindowPos(Handle, HwndTopmost, Left, Top, Width, Height, SwpNoActivate | SwpNoOwnerZOrder | SwpShowWindow);
        }

        private static Rectangle ClampRectangle(Rectangle rectangle, Rectangle bounds)
        {
            var width = Math.Min(rectangle.Width, bounds.Width);
            var height = Math.Min(rectangle.Height, bounds.Height);
            var left = Math.Clamp(rectangle.Left, bounds.Left, bounds.Right - width);
            var top = Math.Clamp(rectangle.Top, bounds.Top, bounds.Bottom - height);
            return new Rectangle(left, top, width, height);
        }

        private static GraphicsPath CreateLensPath(int lensSize)
        {
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, lensSize, lensSize);
            return path;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
