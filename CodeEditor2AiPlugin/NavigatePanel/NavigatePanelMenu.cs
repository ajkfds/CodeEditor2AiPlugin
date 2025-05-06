using Avalonia.Controls;
using CodeEditor2.Data;
using CodeEditor2.NavigatePanel;
using CodeEditor2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginAi.NavigatePanel
{
    public static class NavigatePanelMenu
    {
        public static void Register()
        {
            MenuItem? menuItem_Add = CodeEditor2.Controller.NavigatePanel.GetContextMenuItem(new List<string> { "Add" });
            if (menuItem_Add == null) return;
            {
                MenuItem menuItem_AddFile = CodeEditor2.Global.CreateMenuItem("ai chat", "MenuItem_AddAiChat",
                    "CodeEditor2AiPlugin/Assets/Icons/chat.svg",
                    Avalonia.Media.Color.FromArgb(100, 200, 240, 240)
                    );
                menuItem_AddFile.Click += menuItem_AddChatLog_Click;
                menuItem_Add.Items.Add(menuItem_AddFile);
            }
        }
        private static async void menuItem_AddChatLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await generateFile("chat log file", "clg",
                (sw, name) =>
                {
                }
            );
        }
        private static string getRelativeFolderPath(NavigatePanelNode node)
        {
            FileNode? fileNode = node as FileNode;
            if (fileNode != null)
            {
                NavigatePanelNode? parentNode = fileNode.Parent as NavigatePanelNode;
                if (parentNode == null) throw new System.Exception();
                return getRelativeFolderPath(parentNode);
            }

            FolderNode? folderNode = node as FolderNode;
            if (folderNode != null)
            {
                return folderNode.Folder.RelativePath;
            }
            return "";
        }

        private static async Task generateFile(
            string typeName,
            string extension,
            Action<System.IO.StreamWriter, string> streamWriter
            )
        {
            NavigatePanelNode? node = CodeEditor2.Controller.NavigatePanel.GetSelectedNode();
            if (node == null) return;

            Project project = CodeEditor2.Controller.NavigatePanel.GetProject(node);

            string relativePath = getRelativeFolderPath(node);
            if (!relativePath.EndsWith(System.IO.Path.DirectorySeparatorChar)) relativePath += System.IO.Path.DirectorySeparatorChar;

            CodeEditor2.Tools.InputWindow window = new CodeEditor2.Tools.InputWindow("Create new " + typeName, "new " + typeName + " name");
            await window.ShowDialog(Controller.GetMainWindow());

            if (window.Cancel) return;
            string name = window.InputText.Trim();

            // duplicate check
            //ProjectProperty? projectProperty = project.ProjectProperties[Plugin.StaticID] as pluginVerilog.ProjectProperty;
            //if (projectProperty == null) return;
            //BuildingBlock? buildingBlock = projectProperty.GetBuildingBlock(name);
            //if (buildingBlock != null)
            //{
            //    CodeEditor2.Controller.AppendLog("Duplicate BuildingBlock Name ;" + name);
            //    return;
            //}

            // create file
            string fileName = name + "." + extension;
            string path = project.GetAbsolutePath(relativePath + fileName);
            if (System.IO.File.Exists(path))
            {
                CodeEditor2.Controller.AppendLog("! already exist " + path, Avalonia.Media.Colors.Red);
            }
            else
            {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(path))
                {
                    streamWriter(sw, name);
                }
            }

            CodeEditor2.Controller.NavigatePanel.UpdateFolder(node);
        }
    }
}
