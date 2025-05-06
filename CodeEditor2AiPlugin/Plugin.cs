using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginAi
{
    public class Plugin : CodeEditor2Plugin.IPlugin
    {
        public static string StaticID = "AIChat";
        public string Id { get { return StaticID; } }

        public bool Register()
        {
            // register filetypes
            {
                FileTypes.ChatLogFile fileType = new FileTypes.ChatLogFile();
                CodeEditor2.Global.FileTypes.Add(fileType.ID, fileType);
            }

            //if (!CodeEditor2.Global.ProjectPropertyDeserializers.ContainsKey(Id))
            //{
            //    CodeEditor2.Global.ProjectPropertyDeserializers.Add(Id,
            //        (je, op) => { return ProjectProperty.DeserializeSetup(je, op); }
            //        );
            //}

            // register project property creator
            //CodeEditor2.Data.Project.Created += projectCreated;

            return true;
        }

        //private void projectCreated(CodeEditor2.Data.Project project)
        //{
        //    project.ProjectProperties.Add(Id, new ProjectProperty(project));
        //}


        public bool Initialize()
        {
            {
                MenuItem menuItem = CodeEditor2.Controller.Menu.Tool;
                //MenuItem newMenuItem = CodeEditor2.Global.CreateMenuItem(
                //    "Create Snapshot",
                //    "menuItem_CreateSnapShot",
                //    "CodeEditor2/Assets/Icons/play.svg",
                //    Avalonia.Media.Colors.Red
                //    );
                //menuItem.Items.Add(newMenuItem);
                //newMenuItem.Click += MenuItem_CreateSnapShot_Click;
            }

            chatControl = new Views.ChatControl();
            chatTab = new TabItem()
            {
                Header = "AI Chat",
                Name = "AIChat",
                FontSize = 12,
                //                Icon = new Avalonia.Media.Imaging.Bitmap("CodeEditor2AiPlugin/Assets/Icons/chat.svg"),

                Content = chatControl
            };

            CodeEditor2.Controller.Tabs.AddItem(chatTab);

            //ContextMenu contextMenu = Controller.NavigatePanel.GetContextMenu();
            //{
            //    //MenuItem menuItem_RunSimulation = CodeEditor2.Global.CreateMenuItem("Run Simulation", "menuItem_RunSimulation","play",Avalonia.Media.Colors.Red);
            //    //contextMenu.Items.Add(menuItem_RunSimulation);
            //    //menuItem_RunSimulation.Click += MenuItem_RunSimulation_Click;
            //}
            // register project property form tab
            //            CodeEditor.Tools.ProjectPropertyForm.FormCreated += Tools.ProjectPropertyTab.ProjectPropertyFromCreated;

            NavigatePanel.NavigatePanelMenu.Register();

            return true;
        }
        internal static Avalonia.Controls.TabItem? chatTab;
        internal static pluginAi.Views.ChatControl? chatControl;

    }
}