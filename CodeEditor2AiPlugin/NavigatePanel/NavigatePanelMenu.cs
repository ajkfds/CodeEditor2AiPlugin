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
        /// <summary>
        /// Register the menu customization for the NavigatePanel.
        /// </summary>
        public static void Register()
        {
            // add the menu customization to the FolderNode context menu
            CodeEditor2.NavigatePanel.FolderNode.CustomizeNavigateNodeContextMenu += Customize;
        }
        public static void Customize(ContextMenu contextMenu)
        {
            MenuItem? menuItem_Add = contextMenu.Items.FirstOrDefault(m =>
            {
                if (m is not MenuItem) return false;
                MenuItem? menuItem = m as MenuItem;
                if (menuItem == null) return false;
                if (menuItem.Header is string && (string)menuItem.Header == "Add") return true;
                return false;
            }) as MenuItem;
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
            if (folderNode != null&& folderNode.Folder != null)
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

            // create file
            string fileName = name + "." + extension;
            string path = project.GetAbsolutePath(relativePath + fileName);
            if (System.IO.File.Exists(path))
            {
                CodeEditor2.Controller.AppendLog("! already exist " + path, Avalonia.Media.Colors.Red);
            }
            else
            {
                try
                {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(path))
                    {
                        streamWriter(sw, name);
                    }
                }catch(Exception ex)
                {
                    Controller.AppendLog("** error : NavigatePanelMenu.generateFile (" + path + ")", Avalonia.Media.Colors.Red);
                    Controller.AppendLog("* " + ex.Message, Avalonia.Media.Colors.Red);
                }
            }

            CodeEditor2.Controller.NavigatePanel.UpdateFolder(node);
        }
    }
}
