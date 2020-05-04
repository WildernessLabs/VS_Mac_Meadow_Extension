using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Gtk;
using Meadow.CLI.DeviceMonitor;
using Meadow.Sdks.IdeExtensions.Vs4Mac.Gui;
using MeadowCLI.DeviceManagement;
using MeadowCLI.Hcom;
using MonoDevelop.Core;
using Pango;
using System.IO;
using System.Threading.Tasks;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class MeadowPadWidget : Gtk.Bin
    {
        MeadowSerialDevice meadowSerialDevice;
        const string spanRed = "<span color=\"red\">";
        const string spanGreen = "<span color=\"green\">";
        const string spanBlue = "<span color=\"blue\">";

        public TextTag tagNormal;
        public TextTag tagYellow;
        public TextTag tagRed;
        public TextTag tagRedBold;
        public TextTag tagGreen;
        public TextTag tagBlueBold;
        public TextTag tagBlue;

        public TextTag tagBlueDark;
        public TextTag tagGrey;


        TextIter iter;
        MeadowPad meadowPad;

        Lazy<MeadowOS> meadowOS;

        private ProgressMonitor outputMonitor;

        public MeadowPadWidget(MeadowSerialDevice meadow, MeadowPad meadowPad)
        {
            meadowSerialDevice = meadow;
            this.meadowPad = meadowPad;
            this.Build();

            meadowOS = new Lazy<MeadowOS>(() => new MeadowOS(meadowPad));

            Gdk.Pixbuf image = Gdk.Pixbuf.LoadFromResource("Meadow.Sdks.IdeExtensions.Vs4Mac.MeadowLogo.png");

            // Gtk.ListStore list = new Gtk.ListStore(typeof(Gdk.Pixbuf));
            image = image.ScaleSimple(30, 20, Gdk.InterpType.Bilinear);

            image1.Pixbuf = image;


            iter = textview1.Buffer.EndIter;
            this.textview1.ModifyFont(FontDescription.FromString(MonoDevelop.Ide.Fonts.FontService.MonospaceFontName));
            this.labelState.ModifyFont(FontDescription.FromString(MonoDevelop.Ide.Fonts.FontService.MonospaceFontName));

            LoadTags();

            textview1.SizeAllocated += Textview1_SizeAllocated;
            meadowSerialDevice.OnMeadowMessage += MeadowData;
           
   
            //Textview background. Cant get this to work
            //textview1.Realized += (sender, e) =>
            //{
            //    Gdk.Pixbuf image = Gdk.Pixbuf.LoadFromResource("Meadow.Sdks.IdeExtensions.Vs4Mac.MeadowLogo.png");
            //    Gdk.Pixmap pixmap, pixmap_mask;
            //    image.RenderPixmapAndMask(out pixmap, out pixmap_mask, 0);
            //    textview1.GdkWindow.SetBackPixmap(pixmap, false);
            //};

        }


        void LoadTags()
        {

            tagYellow = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,
                Background = "yellow",
                Foreground = "black"
            };
            this.textview1.Buffer.TagTable.Add(tagYellow);

            tagRedBold = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,
                Foreground = "red"
            };
            this.textview1.Buffer.TagTable.Add(tagRedBold);

            tagRed = new TextTag(null)
            {
                Foreground = "red"
            };
            this.textview1.Buffer.TagTable.Add(tagRed);

            tagGreen = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,
                Foreground = "green"
            };
            this.textview1.Buffer.TagTable.Add(tagGreen);

            tagBlueBold = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,
                Foreground = "blue"
            };
            this.textview1.Buffer.TagTable.Add(tagBlueBold);

            tagBlue = new TextTag(null)
            {
                Foreground = "blue"
            };
            this.textview1.Buffer.TagTable.Add(tagBlue);

            tagBlueDark = new TextTag(null)
            {
                Foreground = "#000080"
            };
            this.textview1.Buffer.TagTable.Add(tagBlueDark);

            tagGrey = new TextTag(null)
            {
                Foreground = "darkgray"
            };
            this.textview1.Buffer.TagTable.Add(tagGrey);
        }

        void Textview1_SizeAllocated(object o, SizeAllocatedArgs args)
        {
            textview1.ScrollToIter(textview1.Buffer.EndIter, 0, false, 0, 0);
        }


        internal void SetRunState(bool runState)
        {
            Gtk.Application.Invoke(delegate
            {
                if (runState)
                {
                    radioMonoEnabled.Active = true;
                }
                else
                {
                    radioMonoDisabled.Active = true;
                }
            });
        }

        public void SetStatus(string text)
        {
            Gtk.Application.Invoke(delegate
            {
                labelState.Markup = text;
            });
        }


        public void SetStatus(MeadowSerialDevice.DeviceStatus status)
        {
            Gtk.Application.Invoke(delegate
            {
                textview1.Buffer.InsertWithTags(ref iter, " STATUS ", tagYellow);

                if (meadowSerialDevice.connection == null)
                {
                    textview1.Buffer.InsertWithTags(ref iter, "No connection\n", tagRedBold);
                    labelState.Markup = $"{spanRed}No connection</span>";
                }
                else if (meadowSerialDevice.connection.Removed)
                {
                    textview1.Buffer.InsertWithTags(ref iter, "Removed\n", tagRedBold);
                    labelState.Markup = $"{spanRed}Removed</span>";
                }
                else
                {
                    var sb = new StringBuilder();
                    if (meadowSerialDevice.connection.USB != null) sb.Append("USB -> ");
                    if (meadowSerialDevice.connection.IP != null) sb.Append("IP -> ");

                    if (meadowSerialDevice.connection.Mode == MeadowMode.MeadowBoot)
                    {
                        textview1.Buffer.InsertWithTags(ref iter, "BOOT mode\n", tagRedBold);
                        sb.Append($"{spanRed}BOOT mode</span>");
                    }
                    else
                    {
                        switch (status)
                        {
                            case MeadowSerialDevice.DeviceStatus.Disconnected:
                                textview1.Buffer.InsertWithTags(ref iter, "Disconnected\n", tagRedBold);
                                sb.Append($"{spanRed}Disconnected</span>");
                                break;
                            case MeadowSerialDevice.DeviceStatus.USBConnected:
                                textview1.Buffer.InsertWithTags(ref iter, "Port Closed\n", tagRedBold);
                                sb.Append($"{spanRed}Port Closed</span>");
                                break;
                            case MeadowSerialDevice.DeviceStatus.PortOpen:
                                textview1.Buffer.InsertWithTags(ref iter, "Port Open\n", tagBlueBold);
                                sb.Append($"{spanBlue}Port Open</span>");
                                break;
                            case MeadowSerialDevice.DeviceStatus.PortOpenGotInfo:
                                textview1.Buffer.InsertWithTags(ref iter, "Initalized\n", tagGreen);
                                sb.Append($"{spanGreen}Initalized</span>");
                                SetDeviceInfo(meadowSerialDevice.DeviceInfo);
                                break;
                            case MeadowSerialDevice.DeviceStatus.Reboot:
                                textview1.Buffer.InsertWithTags(ref iter, "Rebooting\n", tagRedBold);
                                sb.Append($"{spanRed}Rebooting</span>");
                                break;
                        }
                        labelState.Markup = sb.ToString();
                    }
                }
            });
        }

        internal void WriteToConsole(string text, TextTag tag = null)
        {
            if (tag == null) tag = tagNormal;
            if (text.StartsWith("ID:")) return;
            if (text.StartsWith("Meadow:")) return;

            Gtk.Application.Invoke(delegate
            {
                textview1.Buffer.InsertWithTags(ref iter, text, tag);
            });
        }

        protected void OnEntry1KeyPressEvent(object o, Gtk.KeyPressEventArgs args)
        {
        }

        internal void SetInfoText(string text)
        {
            Gtk.Application.Invoke(delegate
            {
                labelInfo.Markup = text;
            });
        }

        internal void SetDeviceInfo(MeadowDeviceInfo deviceInfo)
        {

            MeadowOS.GetFirmwareVersion().ContinueWith((arg) =>
            {
                var latestVersion = arg.Result;
                var meadowVersion = new Version(deviceInfo.MeadowOSVersion.Substring(0, (deviceInfo.MeadowOSVersion.IndexOf(' '))));

                var sb = new StringBuilder();

                sb.Append($"{spanBlue}SerialNo:</span> {deviceInfo.SerialNumber} ");
                sb.Append($"{spanBlue}OSVersion:</span> ");

                if (meadowVersion.CompareTo(latestVersion) > 0)
                {
                    sb.Append($"{spanRed}{deviceInfo.MeadowOSVersion}</span> ");
                    WriteToConsole("MeadowOS out of date", tagRedBold);
                }
                else
                {
                    sb.Append($"{spanGreen}{deviceInfo.MeadowOSVersion}</span> ");
                }

                sb.Append($"{spanBlue}Model:</span> {deviceInfo.Model} ");
                sb.Append($"{spanBlue}Proccessor:</span> {deviceInfo.Proccessor} ");

                sb.Append($"{spanBlue}CoProcessor:</span> {deviceInfo.CoProcessor} ");
                sb.Append($"{spanBlue}CoProcessorOs:</span> {deviceInfo.CoProcessorOs} ");
                sb.Append($"{spanBlue}ProcessorId:</span> {deviceInfo.ProcessorId} ");

                Gtk.Application.Invoke(delegate
                {
                    labelInfo.Markup = sb.ToString();
                    meadowPad.Window.Title = $"Meadow {deviceInfo.SerialNumber}";
                });
            });
        }


        protected void OnClearConsoleActionActivated(object sender, EventArgs e)
        {
            textview1.Buffer.Clear();
        }

        protected void OnCheckConsoleDebugLogToggled(object sender, EventArgs e)
        {
            if (((Gtk.ToggleAction)sender).Active)
            {
                meadowSerialDevice.ConsoleOutputText += ConsoleText;
            }
            else
            {
                meadowSerialDevice.ConsoleOutputText -= ConsoleText;
            }
        }

        void ConsoleText(object sender, string text)
        {
            WriteToConsole(text, tagGrey);
        }

        void MeadowData(object sender, MeadowMessageEventArgs args)
        {
            if (!String.IsNullOrEmpty(args.Message))
            {
                switch (args.MessageType)
                {
                    case MeadowMessageType.Data:
                        if (MeadowDataOutputAction.Active) WriteToConsole($"{args.Message}\n", tagBlueDark);
                        break;
                    case MeadowMessageType.AppOutput:
                        if (MeadowAppOutputAction.Active) WriteToConsole($"{args.Message}", tagBlue);
                        break;
                    case MeadowMessageType.MeadowTrace:
                        if (MeadowTraceOutputAction.Active) WriteToConsole($"{args.Message}\n", tagGrey);
                        break;
                }
            }
        }

        protected void OnRadioMonoEnabledPressed(object sender, EventArgs e)
        {
            if (!((Gtk.RadioButton)sender).Active)
                MeadowDeviceManager.MonoEnable(meadowSerialDevice);
        }

        protected void OnRadioMonoDisabledPressed(object sender, EventArgs e)
        {
            if (!((Gtk.RadioButton)sender).Active)
                MeadowDeviceManager.MonoDisable(meadowSerialDevice);
        }

        protected void OnDownloadLatestVersionActionActivated(object sender, EventArgs e)
        {
            meadowOS.Value.Download_Firmware();
        }

        protected void OnFlashMeadowOSActionActivated(object sender, EventArgs e)
        {
            meadowOS.Value.FlashFirmware();
        }

        protected async void OnListFilesActionActivated(object sender, EventArgs e)
        {
        
            textview1.Buffer.InsertWithTags(ref iter, "Requesting Files.", tagNormal);
        
            var fileList = await meadowSerialDevice.GetFilesOnDevice();

            var table = new TextTableDisplay();
            table.AppendCol("Filename");
            table.AppendCharLine('~');            
            
            foreach (var file in fileList)
            {
                table.AppendCol(file);
                table.NewLine();
            }
            if (fileList.Count == 0) table.AppendLine("No files found");
            
            textview1.Buffer.InsertWithTags(ref iter, table.ToString(), tagNormal);
            
        }

        protected async void OnListFilesAndCRCsActionActivated(object sender, EventArgs e)
        {        
            textview1.Buffer.InsertWithTags(ref iter, "Requesting Files.\n", tagNormal);
        
            var fileList = await meadowSerialDevice.GetFilesAndCrcs();

            var table = new TextTableDisplay();
            table.AppendCol("Filename", "CRC");
            table.AppendCharLine('~');            
            
            for (var i=0;i< fileList.files.Count; i++)
            {
                table.AppendCol(fileList.files.ElementAt(i));
                table.AppendCol(fileList.crcs.ElementAt(i).ToString());
                table.NewLine();
            }
            if (fileList.files.Count == 0) table.AppendLine("No files found");
            
            textview1.Buffer.InsertWithTags(ref iter, table.ToString(), tagNormal);
        }

        protected async void OnFileManagerActionActivated(object sender, EventArgs e)
        {

            bool activeLogState = MeadowDataOutputAction.Active;
            if (!activeLogState) MeadowDataOutputAction.Active = true;
            
            var tcs = new TaskCompletionSource<ResponseType>();
            var fileManager = new FileManager(meadowPad);

            fileManager.Response += (o, args) =>
            {
                tcs.SetResult(args.ResponseId);
            };
            fileManager.Run(); //Blocks
            fileManager.HideAll();
            fileManager.Destroy();
            if (!activeLogState) MeadowDataOutputAction.Active = false;
        }
    }
}
