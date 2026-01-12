using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginAi.NavigatePanel
{
    public class ChatLogNode : CodeEditor2.NavigatePanel.FileNode
    {
        public ChatLogNode(Data.ChatLogFile chatlogFile) : base(chatlogFile)
        {

        }

        public virtual Data.ChatLogFile? ChatLogFile
        {
            get { return Item as Data.ChatLogFile; }
        }

        public override async void OnSelected()
        {
            try
            {
                if (pluginAi.Plugin.ChatTabControl == null) throw new Exception();
                if (pluginAi.Plugin.chatTab == null) throw new Exception();

                if (ChatLogFile == null) return;


                Plugin.ChatTabControl.LogFilePath = ChatLogFile.AbsolutePath;
                await Plugin.ChatTabControl.LoadMessagesAsync();
                CodeEditor2.Controller.Tabs.SelectTab(Plugin.chatTab);

                await UpdateAsync();
            }
            catch
            {
                if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
            }
        }

        public override Task UpdateAsync()
        {
            if (ChatLogFile == null)
            {
                return Task.CompletedTask;
            }
            UpdateVisual();
            return Task.CompletedTask;
        }

        public override void UpdateVisual()
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _updateVisual();
                }catch(Exception ex)
                {
                    CodeEditor2.Controller.AppendLog("#Exception " + ex.Message, Avalonia.Media.Colors.Red);
                }
            });
        }
        public void _updateVisual()
        {
            string text = "-";
            if (FileItem != null) text = FileItem.Name;
            Text = text;

            List<CodeEditor2.NavigatePanel.NavigatePanelNode> newNodes = new List<CodeEditor2.NavigatePanel.NavigatePanelNode>();



            List<CodeEditor2.NavigatePanel.NavigatePanelNode> removeNodes = new List<CodeEditor2.NavigatePanel.NavigatePanelNode>();
            lock (Nodes)
            {
                foreach (CodeEditor2.NavigatePanel.NavigatePanelNode node in Nodes)
                {
                    if (!newNodes.Contains(node))
                    {
                        removeNodes.Add(node);
                    }
                }
                foreach (CodeEditor2.NavigatePanel.NavigatePanelNode node in removeNodes)
                {
                    Nodes.Remove(node);
                    node.Dispose();
                }

                foreach (CodeEditor2.NavigatePanel.NavigatePanelNode node in newNodes)
                {
                    if (Nodes.Contains(node)) continue;

                    int index = newNodes.IndexOf(node);
                    Nodes.Insert(index, node);
                }
            }

            if (ChatLogFile == null) return;

            Image = GetIcon(ChatLogFile);

        }
        public static IImage? GetIcon(Data.ChatLogFile chatLogFile)
        {
            // Icon badge will update only in UI thread
            if (System.Threading.Thread.CurrentThread.Name != "UI")
            {
                throw new Exception();
            }

            List<AjkAvaloniaLibs.Libs.Icons.OverrideIcon> overrideIcons = new List<AjkAvaloniaLibs.Libs.Icons.OverrideIcon>();

            //if (verilogRelatedFile.CodeDocument != null && verilogRelatedFile.CodeDocument.IsDirty)
            //{
            //    overrideIcons.Add(new AjkAvaloniaLibs.Libs.Icons.OverrideIcon()
            //    {
            //        SvgPath = "CodeEditor2/Assets/Icons/shine.svg",
            //        Color = Avalonia.Media.Color.FromArgb(255, 255, 255, 200),
            //        OverridePosition = AjkAvaloniaLibs.Libs.Icons.OverridePosition.UpRight
            //    });
            //}

            //if (verilogRelatedFile != null && verilogRelatedFile.VerilogParsedDocument != null)
            //{
            //    if (verilogRelatedFile.VerilogParsedDocument.ErrorCount > 0)
            //    {
            //        overrideIcons.Add(new AjkAvaloniaLibs.Libs.Icons.OverrideIcon()
            //        {
            //            SvgPath = "CodeEditor2VerilogPlugin/Assets/Icons/exclamation_triangle.svg",
            //            Color = Avalonia.Media.Color.FromArgb(255, 255, 20, 20),
            //            OverridePosition = AjkAvaloniaLibs.Libs.Icons.OverridePosition.DownLeft
            //        });
            //    }
            //    else if (verilogRelatedFile.VerilogParsedDocument.WarningCount > 0)
            //    {
            //        overrideIcons.Add(new AjkAvaloniaLibs.Libs.Icons.OverrideIcon()
            //        {
            //            SvgPath = "CodeEditor2VerilogPlugin/Assets/Icons/exclamation_triangle.svg",
            //            Color = Avalonia.Media.Color.FromArgb(255, 255, 255, 20),
            //            OverridePosition = AjkAvaloniaLibs.Libs.Icons.OverridePosition.DownLeft
            //        });
            //    }
            //}

            return AjkAvaloniaLibs.Libs.Icons.GetSvgBitmap(
                "CodeEditor2AiPlugin/Assets/Icons/chat.svg",
                Avalonia.Media.Color.FromArgb(100, 200, 240, 240),
                overrideIcons
                );
        }
    }
}
