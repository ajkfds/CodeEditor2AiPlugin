using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.AI;
using Microsoft.Playwright;
using Microsoft.VisualBasic;
using pluginAi.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TextMateSharp.Internal.Grammars.Parser;
using static System.Net.Mime.MediaTypeNames;

namespace pluginAi
{
    public class LLMChat
    {
        public LLMChat(ILLMChatFrontEnd chatClient)
        {
            chat = chatClient;// new OpenRouterChat(OpenRouterModels.openai_gpt_oss_20b);
        }

        /*
            LLMChat -(ILLMChatFrontEnd)-+-> ChatControl -+--> OpenRouterChat -->  Microsoft.Extentions.AI.ChatClient
                                        +----------------+
        */
        private ILLMChatFrontEnd chat { init; get; }

        /// <summary>
        /// enable debug output
        /// </summary>
        public bool DebugMode { get; set; } = false;
        /// <summary>
        /// function call tools
        /// </summary>
        public List<AITool> Tools { get; } = new List<AITool>();
        /// <summary>
        /// function call is implemented in message
        /// </summary>
        public bool PersudoFunctionCallMode = false;
        /// <summary>
        ///  base prompt for initial message
        /// </summary>
        public string BasePrompt { get; set; } = "";
        /// <summary>
        /// parameters to replace strings in BasePrompt
        /// </summary>
        public Dictionary<string, string> PromptParameters = new Dictionary<string, string>();
        //        public string Role { get; set; } = "a highly skilled software engineer with extensive knowledge in many programming languages, frameworks, design patterns, and best practices";

        public async Task<string?> AskAsync(string prompt, CancellationToken cancellationToken)
        {
            if (DebugMode)
            {
                System.Diagnostics.Debug.WriteLine("---------- AskAsync Q");
                System.Diagnostics.Debug.WriteLine(prompt);
            }
            string ret;
            if (PersudoFunctionCallMode)
            {
                ret = await chat.GetAsyncChatResult(prompt, null, cancellationToken);
            }
            else
            {
                ret = await chat.GetAsyncChatResult(prompt, Tools, cancellationToken);
            }
            if (DebugMode)
            {
                System.Diagnostics.Debug.WriteLine("---------- AskAsync A");
                System.Diagnostics.Debug.WriteLine(ret);
            }

            if (PersudoFunctionCallMode)
            {
                string? funcResult = await ParseExecutePersudoFunctionCall(ret, cancellationToken);
                if (funcResult != null)
                {
                    await AskAsync(funcResult, cancellationToken);
                }
            }

            return ret;
        }

        public void OverrideSend(string text)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancellationToken = cts.Token;
            Dispatcher.UIThread.Invoke(async () => { await AskAsync(text, cancellationToken); });
        }
        public async Task ResetAsync(CancellationToken cancellationToken)
        {
            await chat.ResetAsync();
            if(chat is ChatControl)
            {
                ((ChatControl)chat).OverrideSend = OverrideSend;
            }

            string basePrompt = BasePrompt;
            StringBuilder sb = new StringBuilder();

            sb.Append(BasePrompt);
            if (PersudoFunctionCallMode)
            {
                AppendPersudoFunctionCallInstruction(sb);
            }
            basePrompt = BuildPrompt(sb);
            basePrompt = basePrompt.Replace("\r\n", "\n").Replace("\r", "\n");

            await AskAsync(basePrompt, cancellationToken);
        }

        // Function Call
        private async Task<string?> ParseExecutePersudoFunctionCall(string responce,CancellationToken cancellationToken)
        {
            var match = Regex.Match(responce, @"<(?<tool>\w+)>(?<params>.*?)</\k<tool>>", RegexOptions.Singleline);

            if (match.Success)
            {
                try
                {
                    string toolName = match.Groups["tool"].Value;
                    AITool? selectedTool = Tools.Where((tool) => { return tool.Name == toolName; }).First();
                    if (selectedTool == null) return null;

                    AIFunctionArguments args = new AIFunctionArguments();
                    string innerContent = match.Groups["params"].Value;
                    var paramMatches = Regex.Matches(innerContent, @"<(?<key>\w+)>(?<value>.*?)</\k<key>>");
                    foreach (Match p in paramMatches)
                    {
                        args.Add(p.Groups["key"].Value, p.Groups["value"].Value);
                    }
                    AIFunction? aIFunction = selectedTool as AIFunction;
                    if (aIFunction == null) return "illgal function call";
                    object? ret = await aIFunction.InvokeAsync(args,cancellationToken);
                    string? s_ret = ret?.ToString();
                    if (s_ret != null)
                    {
                        return s_ret;
                    }
                }
                catch
                {
                    return "illagal function call";
                }
            }
            return null;
        }

        // Build Prompt

        private string BuildPrompt(StringBuilder sb)
        {
            string prompt = sb.ToString();

            foreach (var keyValuePair in PromptParameters)
            {
                prompt = prompt.Replace("${"+keyValuePair.Key+"}", keyValuePair.Value);
            }
            prompt = prompt.Replace("\r\n", "\n").Replace("\r", "\n");
            return prompt;
        }
        private void AppendPersudoFunctionCallInstruction(StringBuilder sb)
        {

            sb.AppendLine("# Tools");
            sb.AppendLine("");

            foreach (var tool in Tools)
            {
                AppendAIToolInstruction(sb, tool); 
            }
        }

        public void AppendAIToolInstruction(StringBuilder sb, AITool tool)
        {
            if (tool is AIFunction aiFunc)
            {
                sb.AppendLine("## " + tool.Name);
                sb.AppendLine("Description: " + tool.Description);

                sb.AppendLine("Parameters:");

                JsonElement schema = aiFunc.JsonSchema;
                StringBuilder usage = new StringBuilder();

                if (schema.TryGetProperty("properties", out var properties))
                {
                    foreach (var prop in properties.EnumerateObject())
                    {
                        string name = prop.Name;
                        string type = prop.Value.GetProperty("type").GetString() ?? "unknown";

                        string? description = prop.Value.TryGetProperty("description", out var desc)
                            ? desc.GetString() : "no description";

                        bool isRequired = false;
                        if (schema.TryGetProperty("required", out var requiredList))
                        {
                            isRequired = requiredList.EnumerateArray().Any(x => x.GetString() == name);
                        }
                        sb.AppendLine("-" + name + ":" + (isRequired ? "(required)" : "(optional)") + description);
                        usage.AppendLine("<"+name+">"+ description +"</" + name + ">");
                    }
                }
                sb.AppendLine("Usage:");
                sb.AppendLine("<" + tool.Name + " >");
                sb.Append(usage.ToString());
                sb.AppendLine("</" + tool.Name + " >");
                sb.AppendLine("");

            }
        }
    }
}
