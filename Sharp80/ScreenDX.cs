﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D10;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

using Color = SharpDX.Color;
using DXBitmap = SharpDX.Direct2D1.Bitmap;

namespace Sharp80
{
    internal sealed class ScreenDX : IScreen
    {
        private const Format PixelFormat = Format.R8G8B8A8_UNorm;

        private Computer computer;
        private TextFormat textFormat, statusTextFormat;
        private Size2F Size { get; set; }
        private RenderTarget renderTarget;
        private IAppWindow parent;
        private SwapChain swapChain;
        private SwapChainDescription swapChainDescription;
        private RenderTargetView backBufferView;
        private Texture2D backBuffer;
        private SharpDX.Direct3D10.Device1 device3D;
        private SharpDX.Direct2D1.Factory d2DFactory;

        private bool advancedView =       false;
        private bool initialized =        false;
        private bool invalid =            true;
        private bool invalidateNextDraw = false;
        private bool erase =              false;
        private bool isDrawing =          false;
        private bool isDisposed =         false;
        private int  isResizing =         0;

        private string statusMessage = String.Empty;
        private int cyclesForMessageRemaining = 0;
        private readonly uint messageDisplayDuration;

        private DXBitmap[] charGenNormal, charGenWide, charGenKanji, charGenKanjiWide;
        private RawRectangleF infoRect, z80Rect, disassemRect, statusMsgRect;
        private RawRectangleF[] cellsNormal, cellsWide;

        private byte[] shadowScreen;
        private bool shadowIsStdWidth;

        private SolidColorBrush foregroundBrush,
                                foregroundBrushWhite,
                                foregroundBrushGreen,
                                backgroundBrush,
                                driveOnBrush,
                                driveActiveBrush;

        private Ellipse driveLightEllipse;
        private bool isFullScreen = false;
        private bool isGreenScreen = false;
        private bool isWideCharMode = false;
        private bool isKanjiCharMode = false;

        // CONSTRUCTOR

        public ScreenDX(bool AdvancedView, uint MessageDisplayDuration, bool GreenScreen)
        {
            advancedView = AdvancedView;
            messageDisplayDuration = MessageDisplayDuration;
            isGreenScreen = GreenScreen;

            cellsNormal = new RawRectangleF[ScreenMetrics.NUM_SCREEN_CHARS];
            cellsWide =   new RawRectangleF[ScreenMetrics.NUM_SCREEN_CHARS];
            shadowScreen = new byte[ScreenMetrics.NUM_SCREEN_CHARS];

            View.OnUserCommand += UserCommandHandler;
        }

        // EVENT HANDLING

        public void SetVideoMode(bool? IsWide, bool? IsKanji)
        {
            // In basic, "PRINT CHR$(23)" (or Shift-RightArrow) will set wide character mode
            // The CLEAR key will revert to normal width
            // And "PRINT CHR$(22)" will toggle the normal and Kanji sets

            if (IsWide.HasValue)
                isWideCharMode = IsWide.Value;
            if (IsKanji.HasValue)
                isKanjiCharMode = IsKanji.Value;
            
            Invalidate();
        }
        public string StatusMessage
        {
            private get { return statusMessage; }
            set
            {
                statusMessage = value;
                if (value.Length == 0)
                    cyclesForMessageRemaining = 0;
                else
                    cyclesForMessageRemaining = (int)messageDisplayDuration;
                Invalidate();
            }
        }
        private void UserCommandHandler(UserCommand Command)
        {
            switch (Command)
            {
                case UserCommand.ToggleAdvancedView:
                    AdvancedView = !AdvancedView;
                    break;
                case UserCommand.ShowAdvancedView:
                    AdvancedView = true;
                    break;
                case UserCommand.GreenScreen:
                    Settings.GreenScreen = GreenScreen = !GreenScreen;
                    break;
            }
        }
        
        // INITIALIZATION / RESET

        public void Initialize(IAppWindow Parent)
        {
            if (initialized)
                throw new Exception();

            // need to do this before computing targetsize
            parent = Parent;
            parent.BackColor = System.Drawing.Color.Black;
            parent.ResizeBegin += (o, args) => { isResizing++; };
            parent.ResizeEnd += (o, args) =>
            {
                isResizing = Math.Max(0, isResizing - 1);
                DoLayout();
            };
            parent.Sizing += (o, args) => { ConstrainAspectRatio(args.Message); };

            Size = DesiredLogicalSize;

            InitializeDX();

            LoadCharGen();

            SetVideoMode(false, false);

            initialized = true;
            Invalidate();
        }
        public void Reinitialize(Computer Computer)
        {
            computer = Computer;
            Reset();
        }
        public void Reset()
        {
            if (isWideCharMode || isKanjiCharMode)
                SetVideoMode(false, false);
        }

        // PUBLIC PROPERTIES

        public bool GreenScreen
        {
            get => isGreenScreen;
            set
            {
                if (value != isGreenScreen)
                {
                    isGreenScreen = value;
                    StatusMessage = "Changing screen color...";
                    Invalidate();

                    LoadCharGen();
                    foregroundBrush = isGreenScreen ? foregroundBrushGreen : foregroundBrushWhite;

                    StatusMessage = "Screen color changed.";
                    erase = true;
                    Invalidate();
                }
            }
        }
        public bool IsFullScreen
        {
            get => isFullScreen;
            set
            {
                if (isFullScreen != value)
                {
                    isFullScreen = value;
                    Resize(DesiredLogicalSize);
                }
            }
        }
        public bool AdvancedView
        {
            get => advancedView;
            set
            {
                if (advancedView != value)
                {
                    advancedView = value;
                    Settings.AdvancedView = value;

                    if (!IsFullScreen)
                        parent.ClientSize =
                            new Size((int)(parent.ClientSize.Height * (advancedView ? ScreenMetrics.WINDOWED_ASPECT_RATIO_ADVANCED : ScreenMetrics.WINDOWED_ASPECT_RATIO_NORMAL)),
                                     parent.ClientSize.Height);
                    Resize(DesiredLogicalSize);
                }
            }
        }

        // DX INTEROP

        private void InitializeDX()
        {
            swapChainDescription = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription((int)Size.Width,
                                                      (int)Size.Height,
                                                      new Rational(60, 1),
                                                      Format.B8G8R8A8_UNorm),
                IsWindowed = true,
                OutputHandle = parent.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput,
                Flags = SwapChainFlags.AllowModeSwitch //?
            };

            // Create Device and SwapChain
            SharpDX.Direct3D10.Device1.CreateWithSwapChain(DriverType.Hardware,
                                                           DeviceCreationFlags.BgraSupport,
                                                           swapChainDescription,
                                                           SharpDX.Direct3D10.FeatureLevel.Level_10_0,
                                                           out device3D,
                                                           out swapChain);

            // Ignore all windows events
            swapChain.GetParent<SharpDX.DXGI.Factory>().MakeWindowAssociation(parent.Handle,
                                                                              WindowAssociationFlags.IgnoreAll);

            CreateBackBuffer();
            CreateRenderTarget(Format.Unknown);

            var directWriteFactory = new SharpDX.DirectWrite.Factory();

            foregroundBrushWhite = new SolidColorBrush(renderTarget, Color.White);
            foregroundBrushGreen = new SolidColorBrush(renderTarget, new RawColor4(0.3f, 1.0f, 0.3f, 1f));
            backgroundBrush = new SolidColorBrush(renderTarget, Color4.Black);
            driveOnBrush = new SolidColorBrush(renderTarget, new RawColor4(0.4f, 0.4f, 0.4f, 0.3f));
            driveActiveBrush = new SolidColorBrush(renderTarget, new RawColor4(1f, 0, 0, 0.3f));

            foregroundBrush = GreenScreen ? foregroundBrushGreen : foregroundBrushWhite;

            textFormat = new TextFormat(directWriteFactory, "Consolas", 12)
            {
                WordWrapping = WordWrapping.NoWrap,
                TextAlignment = TextAlignment.Leading
            };
            statusTextFormat = new TextFormat(directWriteFactory, "Calibri", 18)
            {
                WordWrapping = WordWrapping.NoWrap,
                TextAlignment = TextAlignment.Trailing
            };

            renderTarget.TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode.Cleartype;

            DoLayout();
        }
        private void CreateRenderTarget(Format Format)
        {
            d2DFactory = d2DFactory ?? new SharpDX.Direct2D1.Factory();

            using (var surface = backBuffer.QueryInterface<Surface>())
            {
                var rtp = new RenderTargetProperties(new PixelFormat(Format, SharpDX.Direct2D1.AlphaMode.Premultiplied));
                renderTarget = new RenderTarget(d2DFactory, surface, rtp);
            }
        }
        private void CreateBackBuffer()
        {
            // Get the backbuffer from the swapchain
            backBuffer = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);

            // Renderview on the backbuffer
            backBufferView = new RenderTargetView(device3D, backBuffer);
        }

        // SPRITE SETUP
        
        private void LoadCharGen()
        {
            charGenNormal = charGenNormal ?? new DXBitmap[0x100];
            charGenWide = charGenWide ?? new DXBitmap[0x100];
            charGenKanji = charGenKanji ?? new DXBitmap[0x100];
            charGenKanjiWide = charGenKanjiWide ?? new DXBitmap[0x100];

            uint filterABGR = GreenScreen ? 0xFF40FF40 : 0xFFFFFFFF;

            var properties = new BitmapProperties(new PixelFormat(PixelFormat,
                                                                  SharpDX.Direct2D1.AlphaMode.Premultiplied));

            byte[] b = Resources.CharGenBase;
            var ms = new System.IO.MemoryStream(b);
            for (int i = 0; i < 0xC0; i++)
                charGenKanji[i] = charGenNormal[i] = CreateBitmap(renderTarget, ms, filterABGR, false, properties);

            byte[] h = Resources.CharGenHigh;
            ms = new System.IO.MemoryStream(h);
            for (int i = 0xC0; i < 0x100; i++)
                charGenNormal[i] = CreateBitmap(renderTarget, ms, filterABGR, false, properties);
            
            byte[] k = Resources.CharGenKanji;
            ms = new System.IO.MemoryStream(k);
            for (int i = 0xC0; i < 0x100; i++)
                charGenKanji[i] = CreateBitmap(renderTarget, ms, filterABGR, false, properties);
            
            ms = new System.IO.MemoryStream(b.Double());
            for (int i = 0; i < 0xC0; i++)
                charGenKanjiWide[i] = charGenWide[i] = CreateBitmap(renderTarget, ms, filterABGR, true, properties);

            ms = new System.IO.MemoryStream(h.Double());
            for (int i = 0xC0; i < 0x100; i++)
                charGenWide[i] = CreateBitmap(renderTarget, ms, filterABGR, true, properties);

            ms = new System.IO.MemoryStream(k.Double());
            for (int i = 0xC0; i < 0x100; i++)
                charGenKanjiWide[i] = CreateBitmap(renderTarget, ms, filterABGR, true, properties);
        }
        private static DXBitmap CreateBitmap(RenderTarget renderTarget, System.IO.MemoryStream MS, uint FilterABGR, bool Wide, BitmapProperties Properties)
        {
            var width = Wide ? ScreenMetrics.CHAR_PIXELS_X * 2 : ScreenMetrics.CHAR_PIXELS_X;
            var size = new Size2(width, ScreenMetrics.CHAR_PIXELS_Y);
            int stride = width * sizeof(int);
            using (var tempStream = new DataStream(ScreenMetrics.CHAR_PIXELS_Y * stride, true, true))
            {
                for (int i = 0; i < width * ScreenMetrics.CHAR_PIXELS_Y; i++)
                    tempStream.Write((MS.ReadByte() == 0) ? 0xFF000000 : FilterABGR);
                tempStream.Position = 0;
                return new DXBitmap(renderTarget, size, tempStream, stride, Properties);
            }
        }

        // LAYOUT

        private void DoLayout()
        {
            float xBorder;
            float yBorder;

            if (IsFullScreen)
            {
                float targetAspect = AdvancedView ? ScreenMetrics.SCREEN_AND_ADV_INFO_ASPECT_RATIO : ScreenMetrics.VIRTUAL_SCREEN_ASPECT_RATIO;
                float logicalAspect = Size.Width / Size.Height;

                if (logicalAspect < targetAspect) // extra vertical space
                {
                    xBorder = 0;
                    var missingXPixels = Size.Height * (targetAspect - logicalAspect);
                    var extraYPixels = missingXPixels / targetAspect;
                    yBorder = extraYPixels / 2;
                }
                else // extra horizontal space
                {
                    xBorder = Size.Height * (logicalAspect - targetAspect) / 2;
                    yBorder = 0;
                }
            }
            else
            {
                xBorder = advancedView ? 12f : 24f;
                yBorder = 12f;
            }

            float xOrigin = xBorder;
            float yOrigin = yBorder;

            for (int j = 0; j < ScreenMetrics.NUM_SCREEN_CHARS_Y; j++)
            {
                for (int i = 0; i < ScreenMetrics.NUM_SCREEN_CHARS_X; i++)
                {
                    float x = i * ScreenMetrics.CHAR_PIXELS_X + xOrigin;
                    float y = j * ScreenMetrics.CHAR_PIXELS_Y + yOrigin;

                    // Cast floats to ints to prevent bleeding at edges of cells when scaling
                    cellsNormal[i + j * ScreenMetrics.NUM_SCREEN_CHARS_X] = new RawRectangleF((int)x, (int)y, (int)(x + ScreenMetrics.CHAR_PIXELS_X), (int)(y + ScreenMetrics.CHAR_PIXELS_Y));
                    cellsWide[i + j * ScreenMetrics.NUM_SCREEN_CHARS_X] = new RawRectangleF((int)x, (int)y, (int)(x + ScreenMetrics.CHAR_PIXELS_X + ScreenMetrics.CHAR_PIXELS_X), (int)(y + ScreenMetrics.CHAR_PIXELS_Y));
                }
            }

            driveLightEllipse = new Ellipse(new RawVector2(6, 6), 5, 5);

            xOrigin += ScreenMetrics.NUM_SCREEN_CHARS_X * ScreenMetrics.CHAR_PIXELS_X + ScreenMetrics.SPACING;

            z80Rect = new RawRectangleF(xOrigin,
                                        yOrigin + ScreenMetrics.SPACING,
                                        xOrigin + ScreenMetrics.Z80WIDTH,
                                        yOrigin + ScreenMetrics.VIRTUAL_SCREEN_HEIGHT - ScreenMetrics.INFO_RECT_HEIGHT - ScreenMetrics.SPACING);

            disassemRect = new RawRectangleF(z80Rect.Right,
                                              z80Rect.Top,
                                              ScreenMetrics.WINDOWED_WIDTH_ADVANCED,
                                              z80Rect.Bottom);

            infoRect = new RawRectangleF(z80Rect.Left,
                                         yOrigin + ScreenMetrics.VIRTUAL_SCREEN_HEIGHT - ScreenMetrics.INFO_RECT_HEIGHT,
                                         z80Rect.Left + ScreenMetrics.ADV_INFO_WIDTH,
                                         yOrigin + ScreenMetrics.VIRTUAL_SCREEN_HEIGHT);

            // Bottom right corner
            statusMsgRect = new RawRectangleF(Size.Width - 175,
                                              Size.Height - 30,
                                              Size.Width - ScreenMetrics.SPACING,
                                              Size.Height);

            erase = true;
        }
        private void Resize(Size2F Size)
        {
            // Wait for draw done
            while (isDrawing)
                System.Threading.Thread.Sleep(0);

            if (!renderTarget.IsDisposed)
                renderTarget.Dispose();

            renderTarget = null;

            // Dispose all previous allocated resources

            backBuffer.Dispose();
            backBufferView.Dispose();

            backBuffer = null;
            backBufferView = null;

            SwapChainFlags flags;

            flags = SwapChainFlags.AllowModeSwitch;

            this.Size = Size;

            // Resize the backbuffer
            try
            {
                swapChain.ResizeBuffers(swapChainDescription.BufferCount,
                                        (int)Size.Width,
                                        (int)Size.Height,
                                        Format.Unknown,
                                        flags);
            }
            catch (Exception ex)
            {
                Log.LogException(ex);
            }
            CreateBackBuffer();
            CreateRenderTarget(Format.Unknown);

            DoLayout();
        }
        private void ConstrainAspectRatio(System.Windows.Forms.Message Msg)
        {
            float ratio;
            if (AdvancedView)
                ratio = ScreenMetrics.WINDOWED_ASPECT_RATIO_ADVANCED;
            else
                ratio = ScreenMetrics.WINDOWED_ASPECT_RATIO_NORMAL;

            float width = parent.ClientSize.Width;
            float height = parent.ClientSize.Height;

            if (Msg.Msg == MessageEventArgs.WM_SIZING)
            {
                var rc = (MessageEventArgs.RECT)Marshal.PtrToStructure(Msg.LParam, typeof(MessageEventArgs.RECT));
                int res = Msg.WParam.ToInt32();
                if (res == MessageEventArgs.WMSZ_LEFT || res == MessageEventArgs.WMSZ_RIGHT)
                {
                    // Left or right resize - adjust height (bottom)
                    rc.Bottom = rc.Top + (int)(width / ratio);
                }
                else if (res == MessageEventArgs.WMSZ_TOP || res == MessageEventArgs.WMSZ_BOTTOM)
                {
                    // Up or down resize - adjust width (right)
                    rc.Right = rc.Left + (int)(height * ratio);
                }
                else if (res == MessageEventArgs.WMSZ_RIGHT + MessageEventArgs.WMSZ_BOTTOM)
                {
                    // Lower-right corner resize -> adjust height (could have been width)
                    rc.Bottom = rc.Top + (int)(width / ratio);
                }
                else if (res == MessageEventArgs.WMSZ_LEFT + MessageEventArgs.WMSZ_TOP)
                {
                    // Upper-left corner -> adjust width (could have been height)
                    rc.Left = rc.Right - (int)(height * ratio);
                }
                Marshal.StructureToPtr(rc, Msg.LParam, true);
            }
        }
        private Size2F DesiredLogicalSize
        {
            get
            {
                float physX, physY;

                if (IsFullScreen)
                {
                    var scn = System.Windows.Forms.Screen.FromHandle(parent.Handle);
                    physX = scn.WorkingArea.Width;
                    physY = scn.WorkingArea.Height;

                    // choose a logical size so that the aspect ratio matches the physical aspect ratio
                    float physicalAspect = physX / physY;
                    float w = ScreenMetrics.VIRTUAL_SCREEN_WIDTH + (advancedView ? ScreenMetrics.ADV_INFO_WIDTH + ScreenMetrics.DISPLAY_SPACING : 0);
                    float h = ScreenMetrics.VIRTUAL_SCREEN_HEIGHT;
                    float targetAspectRatio = w / h;

                    if (physicalAspect > targetAspectRatio) // extra horizontal space
                        w += h * (physicalAspect - targetAspectRatio);
                    else // extra vertical space
                        h = h * targetAspectRatio / physicalAspect;
                    return new Size2F(w, h);
                }
                else
                {
                    return new Size2F(AdvancedView ? ScreenMetrics.WINDOWED_WIDTH_ADVANCED
                                                   : ScreenMetrics.WINDOWED_WIDTH_NORMAL,
                                      ScreenMetrics.WINDOWED_HEIGHT);
                }
            }
        }

        // RENDERING

        public void Render()
        {
            if (DrawOK)
            {
                try
                {
                    isDrawing = true;

                    BeginDraw();
                    Draw();
                    EndDraw();
                }
                catch (Exception ex)
                {
                    ex.Data.Add("ExtraMessage", "Exception in D3D Render Loop");
                    Log.LogException(ex);
                }
                finally
                {
                    isDrawing = false;
                }
            }
        }
        public void Invalidate() => invalid = true;
        private bool DrawOK => isResizing == 0 && !isDrawing && !parent.IsMinimized;
        private void Draw()
        {
            if (initialized)
            {
                invalid |= View.Invalid;

                if (invalidateNextDraw)
                {
                    invalidateNextDraw = false;
                    invalid = true;
                }

                if (erase)
                {
                    invalid = true;
                    renderTarget.Clear(Color.Black);
                }
                else if (AdvancedView)
                {
                    // Erase adv info regions...
                    renderTarget.FillRectangle(infoRect, backgroundBrush);
                    renderTarget.FillRectangle(z80Rect, backgroundBrush);
                    renderTarget.FillRectangle(disassemRect, backgroundBrush);
                }

                var dbs = computer.DriveBusyStatus;
                if (dbs.HasValue)
                    renderTarget.FillEllipse(driveLightEllipse, dbs.Value ? driveActiveBrush : driveOnBrush);
                else
                    renderTarget.FillEllipse(driveLightEllipse, backgroundBrush);

                if (--cyclesForMessageRemaining <= 0)
                    invalid = true;

                // Draw the screen
                if (View.CurrentMode == ViewMode.Normal || invalid)
                    DrawView(View.GetViewData(), erase);

                // Used to debug layout issues: frames the virtual screen
                //renderTarget.DrawRectangle(new RawRectangleF(cells[0].Left, cells[0].Top, cells[0x3ff].Right, cells[0x3ff].Bottom), foregroundBrush);
                //renderTarget.FillRectangle(cells[0], foregroundBrush);
                //renderTarget.FillRectangle(cells[0x3ff], foregroundBrush);

                if (AdvancedView)
                {
                    // And draw new text
                    renderTarget.DrawText(computer.GetInternalsReport(), textFormat, z80Rect, foregroundBrush);
                    renderTarget.DrawText(computer.GetDisassembly(), textFormat, disassemRect, foregroundBrush);
                    renderTarget.DrawText(
                        computer.GetClockReport() + Environment.NewLine + computer.GetIoStatusReport(),
                        textFormat, infoRect, foregroundBrush);
                }
                
                erase = invalid = false;
                View.Validate();
            }
        }
        private void DrawView(IEnumerable<byte> View, bool ForceRedraw)
        {
            bool stdWidth;
            DXBitmap[] charGen;

            int end;
            string msg;

            // Tag the status message at the end if there is one
            if (cyclesForMessageRemaining > 0 || (!AdvancedView && !computer.IsRunning && computer.HasRunYet))
            {
                if (cyclesForMessageRemaining > 0)
                {
                    msg = " " + StatusMessage;
                    end = ScreenMetrics.NUM_SCREEN_CHARS - msg.Length;
                }
                else
                {
                    msg = " Paused";
                    end = ScreenMetrics.NUM_SCREEN_CHARS - 7;
                }
            }
            else
            {
                end = ScreenMetrics.NUM_SCREEN_CHARS;
                msg = String.Empty;
            }

            // Figure out what we are displaying and which char set to use

            if (View == null)
            {
                View = computer.Memory.VideoMemory;
                stdWidth = !isWideCharMode;
                if (stdWidth)
                    charGen = isKanjiCharMode ? charGenKanji : charGenNormal;
                else
                    charGen = isKanjiCharMode ? charGenKanjiWide : charGenWide;
            }
            else
            {
                stdWidth = true;
                charGen = charGenNormal;
            }

            var cells = stdWidth ? cellsNormal : cellsWide ;
            bool drawAll = ForceRedraw || stdWidth != shadowIsStdWidth;

            // and draw it

            int i = 0;
            foreach (byte v in View)
            {
                byte b = (i < end) ? v : (byte)msg[(i - end) / (stdWidth ? 1 : 2)];

                if ((stdWidth || i % 2 == 0) && (drawAll|| shadowScreen[i] != b))
                {
                    renderTarget.DrawBitmap(charGen[b],
                        cells[i],
                        1.0f,
                        BitmapInterpolationMode.Linear);
                    shadowScreen[i] = b;
                }
                i++;
            }
            shadowIsStdWidth = stdWidth;
        }
        private void BeginDraw()
        {
            device3D.Rasterizer.SetViewports(new Viewport(0, 0, (int)Size.Width, (int)Size.Height));
            device3D.OutputMerger.SetTargets(backBufferView);
            renderTarget.BeginDraw();
        }
        private void EndDraw()
        {
            renderTarget.EndDraw();
            swapChain.Present(0, PresentFlags.None);
        }

        // SNAPSHOTS

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(isWideCharMode);
            Writer.Write(isKanjiCharMode);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            SetVideoMode(Reader.ReadBoolean(), Reader.ReadBoolean());
        }

        // CLEANUP

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                if (!backBufferView.IsDisposed)
                    backBufferView.Dispose();
                GC.SuppressFinalize(this);
            }
        }
        ~ScreenDX()
        {
            Dispose();
        }
    }
}
