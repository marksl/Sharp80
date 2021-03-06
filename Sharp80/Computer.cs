﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80
{
    public class Computer : IDisposable
    {
        public const int SERIALIZATION_VERSION = 9;

        private const ushort TAPE_SPEED_SELECT_RAM_LOCATION = 0x4211;

        public bool Ready { get; private set; }
        public bool HasRunYet { get; private set; }

        private Processor.Z80 Processor { get; set; }
        private Clock Clock { get; set; }
        private FloppyController FloppyController { get; set; }
        private PortSet Ports { get; set; }
        private InterruptManager IntMgr { get; set; }
        private IScreen Screen { get; set; }
        private ISound Sound { get; set; }
        private Tape Tape { get; set; }
        private Printer Printer { get; set; }
        private bool isDisposed = false;

        // CONSTRUCTOR

        public Computer(IScreen Screen, bool DiskUserEnabled, bool SoundEnabled = true)
        {
            ulong ticksPerSoundSample = Clock.TICKS_PER_SECOND / SoundX.SAMPLE_RATE;

            HasRunYet = false;

            this.Screen = Screen;

            IntMgr = new InterruptManager(this);
            Tape = new Tape(this);
            Ports = new PortSet(this);
            Processor = new Processor.Z80(this, Ports);
            Printer = new Printer();

            // If sound fails to initialize there might be a driver issue,
            // but it's not fatal: we can continue without sound

            if (SoundEnabled)
            {
                Sound = new SoundX(new GetSampleCallback(Ports.CassetteOut));
                if (Sound.Stopped)
                {
                    Sound.Dispose();
                    Sound = new SoundNull();
                }
            }
            else
            {
                Sound = new SoundNull();
            }

            Clock = new Clock(this,
                              Processor,
                              IntMgr,
                              ticksPerSoundSample,
                              new SoundEventCallback(Sound.Sample));

            Clock.SpeedChanged += (s, e) => Sound.Mute = !Clock.NormalSpeed;

            this.DiskUserEnabled = DiskUserEnabled;

            FloppyController = new FloppyController(this, Ports, Clock, IntMgr, Sound, DiskUserEnabled);

            IntMgr.Initialize(Ports, Tape);
            Tape.Initialize(Clock, IntMgr);
            Ports.Initialize(FloppyController, IntMgr, Tape, Printer);

            Ready = true;
        }

        // PROPERTIES

        /// <summary>
        /// This may vary from Settings.DiskEnabled because we'll disable
        /// the floppy contoller on start if no disk is in drive 0
        /// </summary>
        public bool DiskEnabled => FloppyController.Enabled;
        public bool IsRunning => Clock.IsRunning;
        public bool IsStopped => Clock.IsStopped;
        public ushort ProgramCounter => Processor.PcVal;
        public ulong GetElapsedTStates() =>Clock.ElapsedTStates;

        public IReadOnlyList<byte> Memory => Processor.Memory;
        public SubArray<byte> VideoMemory => Processor.Memory.VideoMemory;

        public ushort BreakPoint
        {
            get => Processor.BreakPoint;
            set => Processor.BreakPoint = value;
        }
        public bool BreakPointOn
        {
            get => Processor.BreakPointOn;
            set => Processor.BreakPointOn = value;
        }
        public bool AltKeyboardLayout
        {
            get => Processor.Memory.AltKeyboardLayout;
            set => Processor.Memory.AltKeyboardLayout = value;
        }
        public bool SoundOn
        {
            get => Sound.On;
            set => Sound.On = value;
        }
        public bool SoundEnabled => !Sound.Stopped;
        public bool DriveNoise
        {
            get => Sound.UseDriveNoise;
            set => Sound.UseDriveNoise = value;
        }
        public bool DiskUserEnabled { set; get; }
        public IZ80_Status CpuStatus
        {
            // Safe to send this out in interface form
            get => Processor;
        }

        // FLOPPY SUPPORT

        public IFloppy GetFloppy(byte DriveNum) => FloppyController.GetFloppy(DriveNum);

        public bool DriveIsUnloaded(byte DriveNum) => FloppyController.DriveIsUnloaded(DriveNum);
        public string GetIoStatusReport()
        {
            return FloppyController.StatusReport +
                   (Tape.MotorOn ? " Tape: " + Tape.StatusReport : String.Empty) +
                   (Printer.HasContent ? " PRT" : String.Empty);
        }
        public bool? DriveBusyStatus => FloppyController.DriveBusyStatus;
        public bool AnyDriveLoaded => FloppyController.AnyDriveLoaded;
        public bool FloppyControllerDrq => FloppyController.Drq;
        public string GetFloppyFilePath(byte DriveNum) => FloppyController.FloppyFilePath(DriveNum);
        public void SetFloppyFilePath(byte DriveNum, string Path)
        {
            var f = FloppyController.GetFloppy(DriveNum);
            if (f != null)
                f.FilePath = Path;
        }
        public IFloppyControllerStatus FloppyControllerStatus => FloppyController;
        public bool DiskHasChanged(byte DriveNum) => FloppyController.DiskHasChanged(DriveNum) ?? false;
        public void SaveFloppy(byte DriveNum) => FloppyController.SaveFloppy(DriveNum);

        // RUN COMMANDS

        public void Start()
        {
            Init();
            Clock.Start();
        }
        public async Task StartAndAwait()
        {
            Start();
            while (!IsRunning)
                await Task.Delay(1);
        }
        private void Init()
        {
            if (!HasRunYet)
            {
                if (!DiskUserEnabled || !FloppyController.Available)
                    FloppyController.Disable();

                HasRunYet = true;
            }
            Sound.Mute = !Clock.NormalSpeed;
        }
        public void Stop(bool WaitForStop)
        {
            Sound.Mute = true;
            Clock.Stop();
            if (WaitForStop)
            {
                while (!Clock.IsStopped)
                    Thread.Sleep(0);     // make sure we're not in the middle of a cycle
            }
        }
        public async Task StopAndAwait()
        {
            Stop(false);
            while (!Clock.IsStopped)
                await Task.Delay(1);
        }
        public void ResetButton() => IntMgr.ResetButtonLatch.Latch();

        public void StepOver()
        {
            if (!IsRunning)
            {
                if (Processor.StepOver())
                    Start();
                else
                    Step();
            }
        }
        public void StepOut()
        {
            if (!IsRunning)
            {
                Processor.SteppedOut = false;
                Start();
            }
        }
        public void Step()
        {
            if (!HasRunYet)
                Init();
            Clock.Step();
        }
        public void Jump(ushort Address)
        {
            Stop(true);
            Processor.Jump(Address);
        }
        public bool NormalSpeed
        {
            get { return Clock.NormalSpeed; }
            set { Clock.NormalSpeed = value; }
        }
        public void Reset()
        {
            if (Ready)
            {
                ResetButton();
                Screen.Reset();
            }
        }
        public bool WideCharMode
        {
            get => Screen.WideCharMode;
            set => Screen.WideCharMode = value;
        }
        public bool AltCharMode
        {
            get => Screen.AltCharMode;
            set => Screen.AltCharMode = value;
        }

        // CALLBACK MANAGEMENT

        /// <summary>
        /// Adds a pulse req and also sets the trigger based on the 
        /// trigger's delay
        /// </summary>
        /// <param name="Req"></param>
        internal void Activate(PulseReq Req) => Clock.ActivatePulseReq(Req);

        /// <summary>
        /// Adds a pulse req without resetting the trigger
        /// </summary>
        /// <param name="Req"></param>
        internal void AddPulseReq(PulseReq Req) => Clock.AddPulseReq(Req);

        // FLOPPY SUPPORT

        internal void StartupInitializeStorage()
        {
            for (byte i = 0; i < 4; i++)
                LoadFloppy(i);

            var tape = Settings.LastTapeFile;
            if (tape.Length > 0 && File.Exists(tape))
                TapeLoad(tape);
        }

        internal void LoadFloppy(byte DriveNum) => LoadFloppy(DriveNum, Storage.GetDefaultDriveFileName(DriveNum));

        public bool LoadFloppy(byte DriveNum, string FilePath)
        {
            bool running = IsRunning;
            bool ret = false;

            if (running)
                Stop(WaitForStop: true);

            switch (FilePath)
            {
                case Storage.FILE_NAME_TRSDOS:
                    LoadTrsDosFloppy(DriveNum);
                    ret = true;
                    break;
                case Storage.FILE_NAME_NEW:
                    LoadFloppy(DriveNum, new DMK(true));
                    ret = true;
                    break;
                case Storage.FILE_NAME_UNFORMATTED:
                    LoadFloppy(DriveNum, new DMK(false));
                    ret = true;
                    break;
                case "":
                    FloppyController.UnloadDrive(DriveNum);
                    ret = true;
                    break;
                default:
                    if (FilePath.StartsWith("\\")) // relative to library
                        FilePath = Path.Combine(Storage.LibraryPath, FilePath.Substring(1));
                    ret = FloppyController.LoadFloppy(DriveNum, FilePath);
                    break;
            }
            if (ret)
                Storage.SaveDefaultDriveFileName(DriveNum, FilePath);
            else if (Storage.GetDefaultDriveFileName(DriveNum) == FilePath)
                Storage.SaveDefaultDriveFileName(DriveNum, String.Empty);

            if (running)
                Start();

            return ret;
        }
        internal void LoadFloppy(byte DriveNum, Floppy Floppy) => FloppyController.LoadFloppy(DriveNum, Floppy);

        public void LoadTrsDosFloppy(byte DriveNum) => LoadFloppy(DriveNum, new DMK(Resources.TRSDOS) { FilePath = Storage.FILE_NAME_TRSDOS });
        
        public void EjectFloppy(byte DriveNum)
        {
            bool running = IsRunning;

            if (running)
                Stop(WaitForStop: true);

            FloppyController.UnloadDrive(DriveNum);

            Storage.SaveDefaultDriveFileName(DriveNum, String.Empty);

            if (running)
                Start();
        }

        // TAPE DRIVE

        public bool TapeLoad(string Path) => Tape.Load(Path);
        public string TapeFilePath { get => Tape.FilePath; set => Tape.FilePath = value; }
        public void TapeLoadBlank() => Tape.LoadBlank();
        public void TapePlay() => Tape.Play();
        public void TapeRecord() => Tape.Record();
        public void TapeRewind() => Tape.Rewind();
        public void TapeEject() => Tape.Eject();
        public void TapeStop() => Tape.Stop();
        public void TapeSave() => Tape.Save();
        public bool TapeChanged => Tape.Changed;
        public bool TapeMotorOnSignal { set => Tape.MotorOnSignal = value; }
        public bool TapeMotorOn => Tape.MotorOn;
        public float TapePercent => Tape.Percent;
        public float TapeCounter => Tape.Counter;
        public TapeStatus TapeStatus => Tape.Status;
        public bool TapeIsBlank => Tape.IsBlank;
        public string TapePulseStatus => Tape.PulseStatus;
        public Baud TapeSpeed => Tape.Speed;

        /// <summary>
        /// Backdoor to get or change the initial user selection at
        /// the "Cass?" prompt
        /// </summary>
        public Baud TapeUserSelectedSpeed
        {
            get => Processor.Memory[TAPE_SPEED_SELECT_RAM_LOCATION] == 0x00 ? Baud.Low : Baud.High;
            set => Processor.Memory[TAPE_SPEED_SELECT_RAM_LOCATION] = (value == Baud.High ? (byte)0xFF : (byte)0x00);
        }

        // PRINTER

        public bool PrinterHasContent => Printer.HasContent;
        public bool PrinterSave() => Printer.Save();
        public string PrinterContent => Printer.PrintBuffer;
        public string PrinterFilePath => Printer.FilePath;
        public void PrinterReset() => Printer.Reset();

        // SNAPSHOTS

        public void SaveSnapshotFile(string FilePath)
        {
            bool running = IsRunning;

            Stop(WaitForStop: true);

            using (BinaryWriter writer = new BinaryWriter(File.Open(FilePath, FileMode.Create)))
            {
                Serialize(writer);
            }

            if (running)
                Start();
        }
        public void LoadSnapshotFile(string FilePath)
        {
            bool running = IsRunning;

            Stop(WaitForStop: true);

            using (BinaryReader reader = new BinaryReader(File.Open(FilePath, FileMode.Open, FileAccess.Read)))
            {
                Deserialize(reader);
            }

            if (running)
                Start();
        }
        private void Serialize(BinaryWriter Writer)
        {
            Writer.Write(SERIALIZATION_VERSION);

            Processor.Serialize(Writer);
            Clock.Serialize(Writer);
            FloppyController.Serialize(Writer);
            IntMgr.Serialize(Writer);
            Screen.Serialize(Writer);
            Tape.Serialize(Writer);
        }
        private bool Deserialize(BinaryReader Reader)
        {
            int ver = Reader.ReadInt32(); // SERIALIZATION_VERSION

            if (ver > SERIALIZATION_VERSION)
            {
                Dialogs.AlertUser($"This snapshot was created with a newer version of {ProductInfo.PRODUCT_NAME}. Please upgrade at {ProductInfo.DOWNLOAD_URL}.");
                return false;
            }
            else if (ver >= 8) // currently supporting v 8 & 9
            {
                if (Processor.Deserialize(Reader, ver)        &&
                    Clock.Deserialize(Reader, ver)            &&
                    FloppyController.Deserialize(Reader, ver) &&
                    IntMgr.Deserialize(Reader, ver)           &&
                    Screen.Deserialize(Reader, ver)           &&
                    Tape.Deserialize(Reader, ver))
                {
                    // ok
                    return true;
                }
                else
                {
                    Dialogs.AlertUser("Data error restoring snapshot.");
                    return false;
                }
            }
            else
            {
                Dialogs.AlertUser($"Snapshot load failed: incompatible snapshot version {ver}.");
                return false;
            }
        }

        // MISC

        public bool LoadCMDFile(CmdFile File)
        {
            Stop(WaitForStop: true);

            if (File.Valid && File.Load(Processor.Memory))
            {
                if (File.ExecAddress.HasValue)
                    Processor.Jump(File.ExecAddress.Value);
                else
                    Processor.Jump(File.LowAddress);
                return true;
            }
            else
            {
                return false;
            }
        }
        public string Disassemble(ushort Start, ushort End, bool MakeAssemblable) => Processor.Disassemble(Start, End, MakeAssemblable);
        public string GetInstructionSetReport() => Processor.GetInstructionSetReport();
        public Assembler.Assembly Assemble(string SourceText) => Processor.Assemble(SourceText);

        public bool HistoricDisassemblyMode
        {
            get => Processor.HistoricDisassemblyMode;
            set => Processor.HistoricDisassemblyMode = value;
        }
        public string GetInternalsReport() => Processor.GetInternalsReport();
        public string GetClockReport() => Clock.GetInternalsReport();
        public string GetDisassembly() => Processor.GetDisassembly();

        // KEYBOARD

        public bool NotifyKeyboardChange(KeyState Key) => Processor.Memory.NotifyKeyboardChange(Key);
        public void ResetKeyboard(bool LeftShift, bool RightShift) => Processor.Memory.ResetKeyboard(LeftShift, RightShift);

        public async Task Paste(string text, CancellationToken Token)
        {
            if (IsRunning)
            {
                foreach (char c in text)
                {
                    var kc = c.ToKeyCode();

                    if (kc.Shifted)
                        NotifyKeyboardChange(new KeyState(KeyCode.LeftShift, true, false, false, true));

                    if (c == '\n')
                        await KeyStroke(kc.Code, kc.Shifted, 1000);
                    else
                        await KeyStroke(kc.Code, kc.Shifted);

                    if (kc.Shifted)
                        NotifyKeyboardChange(new KeyState(KeyCode.LeftShift, true, false, false, false));

                    if (Token.IsCancellationRequested || !IsRunning)
                        break;
                }
            }
        }
        public async Task KeyStroke(KeyCode Key, bool Shifted, uint DelayMSecUp = 70u, uint DelayMSecDown = 70u)
        {
            if (Key != KeyCode.None)
            {
                NotifyKeyboardChange(new KeyState(Key, Shifted, false, false, true));
                await Delay(DelayMSecDown);
                NotifyKeyboardChange(new KeyState(Key, Shifted, false, false, false));
                await Delay(DelayMSecUp);
            }
        }

        // TEST SUPPORT

        public async Task Delay(uint VirtualMSec)
        {
            bool done = false;
            var pr = new PulseReq(PulseReq.DelayBasis.Microseconds, VirtualMSec * 1000, () => { done = true; });
            Clock.ActivatePulseReq(pr);
            while (!done && pr.Active && IsRunning)
                await Task.Delay(NormalSpeed ? (int)Math.Max(5, VirtualMSec / 5) : Math.Max(2, (int)VirtualMSec / 100));
        }
        public void Dispose()
        {
            if (!isDisposed)
            {
                if (Ready)
                {
                    Stop(true);
                    Ready = false;
                }
                FloppyController.Dispose();
                Sound.Dispose();
                Printer.Dispose();
                Stop(WaitForStop: false); // ?
                isDisposed = true;
            }
        }
    }
}
