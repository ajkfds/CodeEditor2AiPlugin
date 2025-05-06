using CodeEditor2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginAi.Data
{
    public class ChatLogFile : CodeEditor2.Data.File
    {
        public static ChatLogFile Create(string relativePath, Project project)
        {
            string name;
            if (relativePath.Contains(System.IO.Path.DirectorySeparatorChar))
            {
                name = relativePath.Substring(relativePath.LastIndexOf(System.IO.Path.DirectorySeparatorChar) + 1);
            }
            else
            {
                name = relativePath;
            }
            ChatLogFile chatLogFile = new ChatLogFile()
            {
                Project = project,
                RelativePath = relativePath,
                Name = name
            };

            return chatLogFile;
        }

        protected override CodeEditor2.NavigatePanel.NavigatePanelNode CreateNode()
        {
            NavigatePanel.ChatLogNode node = new NavigatePanel.ChatLogNode(this);
            return node;
        }
    }
}
