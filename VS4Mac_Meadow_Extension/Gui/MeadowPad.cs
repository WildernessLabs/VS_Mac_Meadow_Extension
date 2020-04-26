using System.IO;
using MeadowCLI.DeviceManagement;
using MonoDevelop.Components;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class MeadowPad : PadContent
    {
        
        public MeadowPadWidget control { get; private set; }
        public MeadowSerialDevice meadowSerialDevice { get; private set; }
        public MeadowDeviceExecutionTarget Target { get; private set; }

        public override Control Control => control; //?? (control = new MeadowPadWidget(meadowSerialDevice));
        //  public override string Id => "MeadowPad";


        public MeadowPad(MeadowDeviceExecutionTarget target, MeadowSerialDevice meadow)
        {
            Target = target;
            meadowSerialDevice = meadow;
            control = new MeadowPadWidget(meadowSerialDevice, this);
        }

        protected override void Initialize(IPadWindow window)
        {
            base.Initialize(window);
            
            StartListeningForWorkspaceChanges();
            //window.PadHidden += (sender, e) => control.SaveNodeLocationsForSelectedProject();

            //Debug.WriteLine($"Bundle path: {NSBundle.MainBundle.BundlePath}");
            //Debug.WriteLine($"Bundle Resource path: {NSBundle.MainBundle.ResourcePath}");
            control.ShowAll();
        }

        void StartListeningForWorkspaceChanges()
        {
            //IdeApp.Workspace.SolutionUnloaded += (sender, e) => control.Clear();
            //IdeApp.Workspace.SolutionLoaded += (sender, e) => control.ReloadProjects();
            //IdeApp.Workspace.ItemAddedToSolution += (sender, e) => control.ReloadProjects();
            //IdeApp.Workspace.ItemRemovedFromSolution += (sender, e) => control.ReloadProjects();
        }
    }
}
