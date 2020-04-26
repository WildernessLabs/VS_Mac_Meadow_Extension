using System;
using System.Collections.Generic;
using Gtk;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac.Gui
{
    public partial class FileManager : Gtk.Dialog
    {
        Gtk.NodeStore Store = new Gtk.NodeStore (typeof (MyTreeNode));
        MeadowPad meadowPad;
        
        
        public FileManager(MeadowPad meadowPad)
        {
            this.meadowPad = meadowPad;
            this.Build();
            
            nodeview1.NodeSelection.Mode = SelectionMode.Multiple;
            var col1 = nodeview1.AppendColumn("File", new CellRendererText(),"text",0);
            BuildList();
            nodeview1.NodeStore = Store;            
            nodeview1.QueueDraw();
            ShowAll();            
        }

        async void BuildList()
        {
            var fileList = await meadowPad.meadowSerialDevice.GetFilesOnDevice();
            Store.Clear();
            foreach (var file in fileList)
            {
                var node = new MyTreeNode(file);
                 Store.AddNode(node);
            }
        }
        
        
        
        protected async void OnButtonAddClicked(object sender, EventArgs e)
        {

            var FileList = new List<string>();
         
            var filechooser = new Gtk.FileChooserDialog("Choose files to upload", null,
                FileChooserAction.Open,
                "Cancel",ResponseType.Cancel,
                "Open",ResponseType.Accept);
            filechooser.SelectMultiple = true;

            if (filechooser.Run() == (int)ResponseType.Accept) 
            {            
                FileList.AddRange(filechooser.Filenames);
            }
        
            filechooser.Destroy();

            if (FileList.Count > 0)
            {
                foreach (var file in FileList)
                {
                    var filename = System.IO.Path.GetFileName(file);
                    await meadowPad.meadowSerialDevice.WriteFile(filename, System.IO.Path.GetDirectoryName(file));
                }
                BuildList();              
            }        
        }

        protected async void OnButtonDeleteClicked(object sender, EventArgs e)
        {            
            var selectedNodes = nodeview1.Selection.GetSelectedRows();

            this.GdkWindow.Cursor = new Gdk.Cursor(Gdk.CursorType.Watch);
            
            foreach (var treePath in selectedNodes)
            {
                var node = (MyTreeNode)Store.GetNode(treePath);
                await meadowPad.meadowSerialDevice.DeleteFile(node.Name);
            }
            BuildList();
            
            this.GdkWindow.Cursor = new Gdk.Cursor(Gdk.CursorType.LeftPtr);        
        }
        
        
        
        [Gtk.TreeNode(ListOnly = true)]
        public class MyTreeNode : Gtk.TreeNode
        {
            public MyTreeNode(string fileName)
            {
                Name = fileName;
            }
    
            [Gtk.TreeNodeValue(Column = 0)]
            public string Name { get; set; }

        }
        
        
        
        
        
        
        
    }
    
    
    
}
