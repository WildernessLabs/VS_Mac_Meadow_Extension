using System;
using System.Text;
using Gtk;
using Meadow.CLI.DeviceMonitor;
using MeadowCLI.DeviceManagement;
using MonoDevelop.Core;
using Pango;

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
        public TextTag tagGreen;
        public TextTag tagBlue;
        
        public TextTag tagPink;
        public TextTag tagBrown;


        TextIter iter;
        
        private ProgressMonitor outputMonitor;

        public MeadowPadWidget(MeadowSerialDevice meadow)
        {
            meadowSerialDevice = meadow;
            this.Build();

            Gdk.Pixbuf image = Gdk.Pixbuf.LoadFromResource("Meadow.Sdks.IdeExtensions.Vs4Mac.MeadowLogo.png");

            // Gtk.ListStore list = new Gtk.ListStore(typeof(Gdk.Pixbuf));
            image = image.ScaleSimple(30, 20, Gdk.InterpType.Bilinear);

            image1.Pixbuf = image;

            
            iter = textview1.Buffer.EndIter;
            this.textview1.ModifyFont(FontDescription.FromString(MonoDevelop.Ide.Fonts.FontService.MonospaceFontName));
            this.labelState.ModifyFont(FontDescription.FromString(MonoDevelop.Ide.Fonts.FontService.MonospaceFontName));
            
            LoadTags();
            
            textview1.SizeAllocated += Textview1_SizeAllocated;
            
            
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
            this.textview1.Buffer.TagTable.Add (tagYellow);
            
            tagRed = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,                
                Foreground = "red"
            };
            this.textview1.Buffer.TagTable.Add (tagRed);
            
            tagGreen = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,                
                Foreground = "green"
            };
            this.textview1.Buffer.TagTable.Add (tagGreen);
            
            tagBlue = new TextTag(null)
            {
                Weight = Pango.Weight.Bold,  
                Foreground = "blue"
            };
            this.textview1.Buffer.TagTable.Add (tagBlue);
            
            
            tagPink = new TextTag(null)
            {
                Foreground = "pink"
            };
            this.textview1.Buffer.TagTable.Add (tagPink);
            
            
            tagBrown = new TextTag(null)
            {
                Foreground = "brown"
            };
            this.textview1.Buffer.TagTable.Add (tagBrown);


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
                    textview1.Buffer.InsertWithTags(ref iter, "No connection\n", tagRed);
                }
                else if (meadowSerialDevice.connection.Removed)
                {
                    textview1.Buffer.InsertWithTags(ref iter, "Removed\n", tagRed);
                }
                else
                {
                    var sb = new StringBuilder();
                    if (meadowSerialDevice.connection.USB != null) sb.Append("USB -> ");
                    if (meadowSerialDevice.connection.IP != null) sb.Append("IP -> ");

                    if (meadowSerialDevice.connection.Mode == MeadowMode.MeadowBoot)
                    {
                        textview1.Buffer.InsertWithTags(ref iter, "BOOT mode\n", tagRed);
                        sb.Append($"{spanRed}BOOT mode</span>");
                    }
                    else
                    {
                        switch (status)
                        {
                            case MeadowSerialDevice.DeviceStatus.Disconnected:
                                textview1.Buffer.InsertWithTags(ref iter, "Disconnected\n", tagRed);
                                sb.Append($"{spanRed}Disconnected</span>");
                                break;
                            case MeadowSerialDevice.DeviceStatus.USBConnected:
                                textview1.Buffer.InsertWithTags(ref iter, "Port Closed\n", tagRed);
                                sb.Append($"{spanRed}Port Closed</span>");
                                break;
                            case MeadowSerialDevice.DeviceStatus.PortOpen:
                                textview1.Buffer.InsertWithTags(ref iter, "Port Open\n", tagBlue);
                                sb.Append($"{spanBlue}Port Open</span>");
                                break;
                            case MeadowSerialDevice.DeviceStatus.PortOpenGotInfo:
                                textview1.Buffer.InsertWithTags(ref iter, "Initalized\n", tagGreen);
                                sb.Append($"{spanGreen}Initalized</span>");
                                SetDeviceInfo(meadowSerialDevice.DeviceInfo);
                                break;
                            case MeadowSerialDevice.DeviceStatus.Reboot:
                                textview1.Buffer.InsertWithTags(ref iter, "Rebooting\n", tagRed);
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


        protected void OnRadioEnabledClicked(object sender, EventArgs e)
        {
            if (((Gtk.RadioButton)sender).Active)
                MeadowDeviceManager.MonoEnable(meadowSerialDevice);
        }

        protected void OnRadioDisabledClicked(object sender, EventArgs e)
        {
            if (((Gtk.RadioButton)sender).Active)
                MeadowDeviceManager.MonoDisable(meadowSerialDevice);
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
            var sb = new StringBuilder();

            sb.Append($"{spanBlue}SerialNo:</span> {deviceInfo.SerialNumber} ");
            sb.Append($"{spanBlue}OSVersion:</span> {deviceInfo.MeadowOSVersion} ");
            sb.Append($"{spanBlue}Model:</span> {deviceInfo.Model} ");
            sb.Append($"{spanBlue}Proccessor:</span> {deviceInfo.Proccessor} ");

            sb.Append($"{spanBlue}CoProcessor:</span> {deviceInfo.CoProcessor} ");
            sb.Append($"{spanBlue}CoProcessorOs:</span> {deviceInfo.CoProcessorOs} ");
            sb.Append($"{spanBlue}ProcessorId:</span> {deviceInfo.ProcessorId} ");

            Gtk.Application.Invoke(delegate
            {
                labelInfo.Markup = sb.ToString();
            });
        }


        protected void OnClearConsoleActionActivated(object sender, EventArgs e)
        {
            textview1.Buffer.Clear();
        }
    }
}
