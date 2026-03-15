using Avalonia.Collections;
using Avalonia.Threading;
using DynamicData;
using FaissNet;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.ML.Data;
using OpenAI.Realtime;
using Svg;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static CodeEditor2.CodeEditor.CodeDocument;
using static pluginAi.OpenRouterModels;
using ModelItem = CodeEditor2.LLM.ModelItem;

namespace pluginAi
{
    public class OpenRouterChat: CodeEditor2.LLM.ILLMChatFrontEnd
    {

        public OpenRouterChat(OpenRouterModels.Model model, bool enableFunctionCalling,bool includeReasoning = false)
        {
            initialize(model,enableFunctionCalling);
            this.EnableFunctionCalling = enableFunctionCalling;
            this.IncludeReasoning = includeReasoning;
        }

        private Microsoft.Extensions.AI.IChatClient client;

        public static string? ApiKey;

        public bool EnableFunctionCalling { get; }
        public bool IncludeReasoning { get; set; }
        private void initialize(OpenRouterModels.Model model,bool enableFunctionCalling)
        {
            if(ApiKey == null)
            {
                CodeEditor2.Controller.AppendLog("Set API Key for OpenRouter",Avalonia.Media.Colors.Red);
                return;
            }

            OpenAI.OpenAIClientOptions openAIClientOptions = new OpenAI.OpenAIClientOptions()
            {
                Endpoint = new System.Uri(
                    "https://openrouter.ai/api/v1"  // openrouter endpoint
                )
            };

            OpenAI.Chat.ChatClient openAiClient = new OpenAI.Chat.ChatClient(
                model: model.Name,
                new ApiKeyCredential(ApiKey),
                openAIClientOptions
                );

            // create IChatClient with OpenAI.Chat.ChatClient
            client = Microsoft.Extensions.AI.OpenAIClientExtensions.AsIChatClient(openAiClient);

            //use user function
            if (enableFunctionCalling)
            {
                client = ChatClientBuilderChatClientExtensions
                   .AsBuilder(client)
                   .UseFunctionInvocation()
                   .Build();
            }
            else
            {
                client = ChatClientBuilderChatClientExtensions
                   .AsBuilder(client)
                   .Build();
            }
        }


        public Task SetModelAsync(OpenRouterModels.Model model,bool enableFunctionCalling)
        {
            initialize(model, enableFunctionCalling);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the list of available models
        /// </summary>
        public List<ModelItem> GetAvailableModels()
        {
            return new List<ModelItem>
            {
                new ModelItem { Id = "openai/gpt-oss-120b", Name = "OpenAI: gpt-oss-120b", Tag = OpenRouterModels.openai_gpt_oss_120b },
                new ModelItem { Id = "deepseek/deepseek-v3.2", Name = "DeepSeek: DeepSeek V3.2", Tag = OpenRouterModels.deepseek_deepseek_v3_2 },
                new ModelItem { Id = "minimax/minimax-m2.5", Name = "MiniMax: MiniMax M2.5", Tag = OpenRouterModels.minimax_minimax_m2_5 },
                new ModelItem { Id = "moonshotai/kimi-k2.5", Name = "MoonshotAI: Kimi K2.5", Tag = OpenRouterModels.moonshotai_kimi_k2_5 },
                new ModelItem { Id = "anthropic/claude-3.7-sonnet", Name = "Anthropic: Claude 3.7 Sonnet", Tag = OpenRouterModels.anthropic_claude_3p7_sonnet },
                new ModelItem { Id = "google/gemini-3-pro-preview", Name = "Google: Gemini 3 Pro Preview", Tag = OpenRouterModels.google_gemini_3_pro_preview },
                new ModelItem { Id = "google/gemini-2.5-flash-lite", Name = "Google: Gemini 2.5 Flash Lite", Tag = OpenRouterModels.google_gemini_2p5_flash_lite },
                new ModelItem { Id = "openai/gpt-oss-20b", Name = "OpenAI: gpt-oss-20b", Tag = OpenRouterModels.openai_gpt_oss_20b },
                new ModelItem { Id = "openai/gpt-4.1-nano", Name = "OpenAI: GPT-4.1 Nano", Tag = OpenRouterModels.openai_gpt_4p1_nano },
                new ModelItem { Id = "xiaomi/mimo-v2-flash:free", Name = "Xiaomi: MiMo-V2-Flash (free)", Tag = OpenRouterModels.xiaomi_mimo_v2_flash_free },
                new ModelItem { Id = "google/gemini-3-flash-preview", Name = "Google: Gemini 3 Flash Preview", Tag = OpenRouterModels.google_gemini_3_flash_preview },
                new ModelItem { Id = "openai/gpt-5.1-codex-mini", Name = "OpenAI: GPT-5.1-Codex-Mini", Tag = OpenRouterModels.openai_gpt_5_1_codex_mini },
                new ModelItem { Id = "openai/gpt-oss-20b:free", Name = "OpenAI: gpt-oss-20b (free)", Tag = OpenRouterModels.openai_gpt_oss_20b_free }
            };
        }

        /// <summary>
        /// Sets the current model from a ModelItem
        /// </summary>
        public Task SetModelAsync(ModelItem modelItem)
        {
            if (modelItem.Tag is OpenRouterModels.Model model)
            {
                initialize(model, EnableFunctionCalling);
            }
            return Task.CompletedTask;
        }

        public List<CodeEditor2.LLM.ChatMessageWrapper> ChatMessageWrappers { get; } = new List<CodeEditor2.LLM.ChatMessageWrapper>();

        public Task ResetAsync()
        {
            ChatMessageWrappers.Clear();
            return Task.CompletedTask;
        }
        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command,IList<AITool>? tools, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ChatOptions options = new ChatOptions();
            if (tools != null && EnableFunctionCalling)
            {
                options = new()
                {
                    Tools = tools
                };
            }
            if (IncludeReasoning)
            {
                options.AdditionalProperties = new() { ["include_reasoning"] = true };
            }
            ChatMessageWrappers.Add(new(ChatRole.User, command));

            List<ChatResponseUpdate> updates = [];

            await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(ChatMessageWrappers, options))
            {
                //foreach (var content in update.Contents)
                //{
                //    if (content is FunctionCallContent call)
                //    {
                //        Console.WriteLine($"\n[Function Call] {call.Name} 縺悟他縺ｳ蜃ｺ縺輔ｌ縺ｾ縺励◆縲ょｼ墓焚: {call.Arguments}");
                //    }

                //    if (content is FunctionResultContent result)
                //    {
                //        Console.WriteLine($"\n[Function Result] 邨先棡縺梧綾繧翫∪縺励◆: {result.Result}");
                //    }

                //    if (content is TextContent text)
                //    {
                //        Console.Write(text.Text);
                //    }
                //}
                yield return update.Text;
                updates.Add(update);
            }


            addMessages(updates);
        }


        private void addMessages(IEnumerable<ChatResponseUpdate> updates)
        {

            if (updates is ICollection<ChatResponseUpdate> { Count: 0 })
            {
                return;
            }

            ChatResponse response = updates.ToChatResponse();

            foreach (var message in response.Messages)
            {
                ChatMessageWrappers.Add(new CodeEditor2.LLM.ChatMessageWrapper(message));
            }
        }



        public async Task<string> GetAsyncChatResult(string command, IList<AITool>? tools, CancellationToken cancellationToken)
        {
            StringBuilder sb = new StringBuilder();
            await foreach (string ret in GetAsyncCollectionChatResult(command, tools, cancellationToken))
            {
                sb.Append(ret);
            }
            return sb.ToString();
        }


        public async Task SaveMessagesAsync( string filePath)
        {
            try
            {

                string serializedData = SerializeMessages(ChatMessageWrappers);
                await using var fs = File.OpenWrite(filePath);
                using var sw = new StreamWriter(fs);
                await sw.WriteAsync(serializedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save error: {ex.Message}");
            }
        }

        
        public async Task LoadMessagesAsync(string filePath)
        {
            ChatMessageWrappers.Clear();
            try
            {
                await using var fs = File.OpenRead(filePath); 
                using var sr = new StreamReader(fs); 
                string text = await sr.ReadToEndAsync();
                List<Microsoft.Extensions.AI.ChatMessage> messages = DeserializeMessages(BinaryData.FromString(text)).ToList();
//                chatMessages = 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load error: {ex.Message}");
            }
        }

        public static IEnumerable<ChatMessage> DeserializeMessages(BinaryData data)
        {
            using JsonDocument messagesAsJson = JsonDocument.Parse(data.ToMemory());

            foreach (JsonElement jsonElement in messagesAsJson.RootElement.EnumerateArray())
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(jsonElement.GetRawText());
                if (message != null)
                {
                    yield return message;
                }
            }

        }
        public static string SerializeMessages(IEnumerable<ChatMessage> messages)
        {
            return JsonSerializer.Serialize(messages);
        }
    }
}
