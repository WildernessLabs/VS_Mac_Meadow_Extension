using System;
using Gtk;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac.Gui
{
    public partial class MeadowSelect : Gtk.Dialog
    {
        Gtk.NodeStore Store = new Gtk.NodeStore (typeof (MyTreeNode)); 
        
        Gdk.Pixbuf icon = Gdk.Pixbuf.LoadFromResource("Meadow.Sdks.IdeExtensions.Vs4Mac.MeadowLogo.png");
        
        public MeadowSelect(IDeploymentTargetsManager deploymentTargets)
        {
            this.Build();

            nodeview1.NodeSelection.Mode = SelectionMode.Single;
            var col1 = nodeview1.AppendColumn("Icon", new CellRendererPixbuf(),"icon",0);
            var col2 = nodeview1.AppendColumn("Device", new CellRendererText(),"text",1);
            BuildList();
            nodeview1.NodeStore = Store;            
            nodeview1.QueueDraw();
            ShowAll();
            
            TreeIter treeIter;
            nodeview1.Model.GetIterFirst(out treeIter);
            var path = nodeview1.Model.GetPath(treeIter);
            nodeview1.SetCursor(path, col2, false);
            
        }


        public MeadowDeviceExecutionTarget GetSelection()
        {
            TreePath treePath;
            TreeViewColumn treeViewColumn;
            
            nodeview1.GetCursor(out treePath, out treeViewColumn);
            var node = (MyTreeNode)Store.GetNode(treePath);

            return node.Target;
        }

        

        void BuildList()
        {
            foreach (var target in MeadowProject.DeploymentTargetsManager.GetTargetList())
            {
                var node = new MyTreeNode(target,icon);
                Store.AddNode(node);
            }
        }

    }



    [Gtk.TreeNode(ListOnly = true)]
    public class MyTreeNode : Gtk.TreeNode
    {
        public MyTreeNode(MeadowDeviceExecutionTarget target, Gdk.Pixbuf icon)
        {
            Target = target;
            Icon = icon;
        }

        public MeadowDeviceExecutionTarget Target { get; private set; }

        [Gtk.TreeNodeValue(Column = 0)]
        public Gdk.Pixbuf Icon;

        [Gtk.TreeNodeValue(Column = 1)]
        public string Name
             {
                get
                {
                    return Target.FullName;
                }
             }
    }
    
    
}
